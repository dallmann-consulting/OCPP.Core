﻿/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2025 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OCPP.Core.Server.Messages_OCPP21;
using OCPP.Core.Database;

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {
        /// <summary>
        /// Waits for new OCPP V2.1 messages on the open websocket connection and delegates processing to a controller
        /// </summary>
        private async Task Receive21(ChargePointStatus chargePointStatus, HttpContext httpContext, OCPPCoreContext dbContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP21");
            ControllerOCPP21 controller21 = new ControllerOCPP21(_configuration, _logFactory, chargePointStatus, dbContext);

            int maxMessageSizeBytes = _configuration.GetValue<int>("MaxMessageSize", 0);

            byte[] buffer = new byte[1024 * 4];
            MemoryStream memStream = new MemoryStream(buffer.Length);

            try
            {
                while (chargePointStatus.WebSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await chargePointStatus.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result != null && result.MessageType != WebSocketMessageType.Close)
                    {
                        logger.LogTrace("OCPPMiddleware.Receive21 => Receiving segment: {0} bytes (EndOfMessage={1} / MsgType={2})", result.Count, result.EndOfMessage, result.MessageType);
                        memStream.Write(buffer, 0, result.Count);

                        // max. allowed message size NOT exceeded - or limit deactivated?
                        if (maxMessageSizeBytes == 0 || memStream.Length <= maxMessageSizeBytes)
                        {
                            if (result.EndOfMessage)
                            {
                                // read complete message into byte array
                                byte[] bMessage = memStream.ToArray();
                                // reset memory stream für next message
                                memStream = new MemoryStream(buffer.Length);

                                string ocppMessage = UTF8Encoding.UTF8.GetString(bMessage);

                                // write message (async) to dump directory
                                _ = Task.Run(() =>
                                {
                                    DumpMessage("ocpp21-in", ocppMessage);
                                });

                                Match match = Regex.Match(ocppMessage, MessageRegExp);
                                if (match != null && match.Groups != null && match.Groups.Count >= 3)
                                {
                                    string messageTypeId = match.Groups[1].Value;
                                    string uniqueId = match.Groups[2].Value;
                                    string action = match.Groups[3].Value;
                                    string jsonPaylod = match.Groups[4].Value;
                                    logger.LogInformation("OCPPMiddleware.Receive21 => OCPP-Message: Type={0} / ID={1} / Action={2})", messageTypeId, uniqueId, action);

                                    OCPPMessage msgIn = new OCPPMessage(messageTypeId, uniqueId, action, jsonPaylod);

                                    // Send raw incoming messages to extensions
                                    _ = Task.Run(() =>
                                    {
                                        ProcessRawIncomingMessageSinks(chargePointStatus.Protocol, chargePointStatus.Id, msgIn);
                                    });

                                    if (msgIn.MessageType == "2")
                                    {
                                        // Request from chargepoint to OCPP server
                                        OCPPMessage msgOut = controller21.ProcessRequest(msgIn, this);

                                        // Send OCPP message with optional logging/dump
                                        await SendOcpp21Message(msgOut, logger, chargePointStatus);
                                    }
                                    else if (msgIn.MessageType == "3" || msgIn.MessageType == "4")
                                    {
                                        // Process answer from chargepoint
                                        if (_requestQueue.ContainsKey(msgIn.UniqueId))
                                        {
                                            controller21.ProcessAnswer(msgIn, _requestQueue[msgIn.UniqueId]);
                                            _requestQueue.Remove(msgIn.UniqueId);
                                        }
                                        else
                                        {
                                            logger.LogError("OCPPMiddleware.Receive21 => HttpContext from caller not found / Msg: {0}", ocppMessage);
                                        }
                                    }
                                    else
                                    {
                                        // Unknown message type
                                        logger.LogError("OCPPMiddleware.Receive21 => Unknown message type: {0} / Msg: {1}", msgIn.MessageType, ocppMessage);
                                    }
                                }
                                else
                                {
                                    logger.LogWarning("OCPPMiddleware.Receive21 => Error in RegEx-Matching: Msg={0})", ocppMessage);
                                }
                            }
                        }
                        else
                        {
                            // max. allowed message size exceeded => close connection (DoS attack?)
                            logger.LogInformation("OCPPMiddleware.Receive21 => Allowed message size exceeded - close connection");
                            await chargePointStatus.WebSocket.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, string.Empty, CancellationToken.None);
                        }
                    }
                    else
                    {
                        logger.LogInformation("OCPPMiddleware.Receive21 => Receive: unexpected result: CloseStatus={0} / MessageType={1}", result?.CloseStatus, result?.MessageType);
                        await chargePointStatus.WebSocket.CloseOutputAsync((WebSocketCloseStatus)3001, string.Empty, CancellationToken.None);
                    }
                }
            }
            finally
            {
                logger.LogInformation("OCPPMiddleware.Receive21 => Websocket closed: State={0} / CloseStatus={1}", chargePointStatus.WebSocket.State, chargePointStatus.WebSocket.CloseStatus);
                _chargePointStatusDict.Remove(chargePointStatus.Id);
            }
        }

        /// <summary>
        /// Sends a (Soft-)Reset to the chargepoint
        /// </summary>
        private async Task Reset21(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP21");
            ControllerOCPP21 controller21 = new ControllerOCPP21(_configuration, _logFactory, chargePointStatus, dbContext);

            Messages_OCPP21.ResetRequest resetRequest = new Messages_OCPP21.ResetRequest();
            resetRequest.Type = Messages_OCPP21.ResetEnumType.OnIdle;
            resetRequest.CustomData = new CustomDataType();
            resetRequest.CustomData.VendorId = ControllerOCPP21.VendorId;

            string jsonResetRequest = JsonConvert.SerializeObject(resetRequest);

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "Reset";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = jsonResetRequest;
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp21Message(msgOut, logger, chargePointStatus);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Sends a Unlock-Request to the chargepoint
        /// </summary>
        private async Task UnlockConnector21(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP21");
            ControllerOCPP21 controller21 = new ControllerOCPP21(_configuration, _logFactory, chargePointStatus, dbContext);

            Messages_OCPP21.UnlockConnectorRequest unlockConnectorRequest = new Messages_OCPP21.UnlockConnectorRequest();
            unlockConnectorRequest.EvseId = 0;
            unlockConnectorRequest.CustomData = new CustomDataType();
            unlockConnectorRequest.CustomData.VendorId = ControllerOCPP21.VendorId;

            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                if (int.TryParse(urlConnectorId, out int iConnectorId))
                {
                    unlockConnectorRequest.EvseId = iConnectorId;
                }
            }
            logger.LogTrace("OCPPMiddleware.OCPP21 => UnlockConnector: ChargePoint='{0}' / EvseId={1}", chargePointStatus.Id, unlockConnectorRequest.EvseId);


            string jsonResetRequest = JsonConvert.SerializeObject(unlockConnectorRequest);

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "UnlockConnector";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = jsonResetRequest;
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp21Message(msgOut, logger, chargePointStatus);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Sends a SetChargingProfile-Request to the chargepoint
        /// </summary>
        private async Task SetChargingProfile21(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId, double power, string unit)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP21");
            ControllerOCPP21 controller21 = new ControllerOCPP21(_configuration, _logFactory, chargePointStatus, dbContext);

            // Parse connector id (int value)
            int connectorId = 0;
            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                int.TryParse(urlConnectorId, out connectorId);
            }

            Messages_OCPP21.SetChargingProfileRequest setChargingProfileRequest = new Messages_OCPP21.SetChargingProfileRequest();
            setChargingProfileRequest.EvseId = connectorId;
            setChargingProfileRequest.ChargingProfile = new Messages_OCPP21.ChargingProfileType();
            // Default values
            setChargingProfileRequest.ChargingProfile.Id = 100;
            setChargingProfileRequest.ChargingProfile.StackLevel = 1;
            setChargingProfileRequest.ChargingProfile.ChargingProfilePurpose = ChargingProfilePurposeEnumType.TxDefaultProfile;
            setChargingProfileRequest.ChargingProfile.ChargingProfileKind = ChargingProfileKindEnumType.Absolute;
            setChargingProfileRequest.ChargingProfile.ValidFrom = DateTime.UtcNow;
            setChargingProfileRequest.ChargingProfile.ValidTo = DateTime.UtcNow.AddYears(1);
            setChargingProfileRequest.ChargingProfile.ChargingSchedule = new List<ChargingScheduleType>()
            {
                new ChargingScheduleType()
                {
                    Id = 101,
                    ChargingRateUnit = string.Equals(unit, "A", StringComparison.InvariantCultureIgnoreCase) ? ChargingRateUnitEnumType.A : ChargingRateUnitEnumType.W,
                    ChargingSchedulePeriod = new List<ChargingSchedulePeriodType>()
                    {
                        new ChargingSchedulePeriodType()
                        {
                            StartPeriod = 0,    // Start 0:00h
                            Limit = power
                        }
                    }
                }
            };

            logger.LogInformation("OCPPMiddleware.OCPP21 => SetChargingProfile: ChargePoint='{0}' / ConnectorId={1} / Power='{2}{3}'", chargePointStatus.Id, setChargingProfileRequest.EvseId, power, unit);

            string jsonResetRequest = JsonConvert.SerializeObject(setChargingProfileRequest);

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "SetChargingProfile";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = jsonResetRequest;
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp21Message(msgOut, logger, chargePointStatus);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Sends a ClearChargingProfile-Request to the chargepoint
        /// </summary>
        private async Task ClearChargingProfile21(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP21");
            ControllerOCPP21 controller21 = new ControllerOCPP21(_configuration, _logFactory, chargePointStatus, dbContext);

            Messages_OCPP21.ClearChargingProfileRequest clearChargingProfileRequest = new Messages_OCPP21.ClearChargingProfileRequest();
            // Default values
            clearChargingProfileRequest.ChargingProfileId = 100;
            clearChargingProfileRequest.ChargingProfileCriteria = new ClearChargingProfileType()
            {
                StackLevel = 1,
                ChargingProfilePurpose = ChargingProfilePurposeEnumType.TxDefaultProfile
            };
            clearChargingProfileRequest.ChargingProfileCriteria.EvseId = 0;
            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                if (int.TryParse(urlConnectorId, out int iConnectorId))
                {
                    clearChargingProfileRequest.ChargingProfileCriteria.EvseId = iConnectorId;
                }
            }
            logger.LogTrace("OCPPMiddleware.OCPP21 => ClearChargingProfile: ChargePoint='{0}' / ConnectorId={1}", chargePointStatus.Id, clearChargingProfileRequest.ChargingProfileCriteria.EvseId);

            string jsonResetRequest = JsonConvert.SerializeObject(clearChargingProfileRequest);

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = "2";
            msgOut.Action = "ClearChargingProfile";
            msgOut.UniqueId = Guid.NewGuid().ToString("N");
            msgOut.JsonPayload = jsonResetRequest;
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>();

            // store HttpContext with MsgId for later answer processing (=> send anwer to API caller)
            _requestQueue.Add(msgOut.UniqueId, msgOut);

            // Send OCPP message with optional logging/dump
            await SendOcpp21Message(msgOut, logger, chargePointStatus);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        private async Task SendOcpp21Message(OCPPMessage msg, ILogger logger, ChargePointStatus chargePointStatus)
        {
            // Send raw outgoing messages to extensions
            _ = Task.Run(() =>
            {
                ProcessRawOutgoingMessageSinks(chargePointStatus.Protocol, chargePointStatus.Id, msg);
            });

            string ocppTextMessage = null;

            if (string.IsNullOrEmpty(msg.ErrorCode))
            {
                if (msg.MessageType == "2")
                {
                    // OCPP-Request
                    ocppTextMessage = string.Format("[{0},\"{1}\",\"{2}\",{3}]", msg.MessageType, msg.UniqueId, msg.Action, msg.JsonPayload);
                }
                else
                {
                    // OCPP-Response
                    ocppTextMessage = string.Format("[{0},\"{1}\",{2}]", msg.MessageType, msg.UniqueId, msg.JsonPayload);
                }
            }
            else
            {
                ocppTextMessage = string.Format("[{0},\"{1}\",\"{2}\",\"{3}\",{4}]", msg.MessageType, msg.UniqueId, msg.ErrorCode, msg.ErrorDescription, "{}");
            }
            logger.LogTrace("OCPPMiddleware.OCPP21 => SendOcppMessage: {0}", ocppTextMessage);

            if (string.IsNullOrEmpty(ocppTextMessage))
            {
                // invalid message
                ocppTextMessage = string.Format("[{0},\"{1}\",\"{2}\",\"{3}\",{4}]", "4", string.Empty, Messages_OCPP21.ErrorCodes.ProtocolError, string.Empty, "{}");
            }

            // write message (async) to dump directory
            _ = Task.Run(() =>
            {
                DumpMessage("ocpp21-out", ocppTextMessage);
            });


            byte[] binaryMessage = UTF8Encoding.UTF8.GetBytes(ocppTextMessage);
            await chargePointStatus.WebSocket.SendAsync(new ArraySegment<byte>(binaryMessage, 0, binaryMessage.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
