/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2024 dallmann consulting GmbH.
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
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP16;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {
        /// <summary>
        /// Waits for new OCPP V1.6 messages on the open websocket connection and delegates processing to a controller
        /// </summary>
        private async Task Receive16(ChargePointStatus chargePointStatus, HttpContext context, OCPPCoreContext dbContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus, dbContext);

            int maxMessageSizeBytes = _configuration.GetValue<int>("MaxMessageSize", 0);

            byte[] buffer = new byte[1024 * 4];
            MemoryStream memStream = new MemoryStream(buffer.Length);

            while (chargePointStatus.WebSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await chargePointStatus.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result != null && result.MessageType != WebSocketMessageType.Close)
                {
                    logger.LogTrace("OCPPMiddleware.Receive16 => Receiving segment: {0} bytes (EndOfMessage={1} / MsgType={2})", result.Count, result.EndOfMessage, result.MessageType);
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
                                DumpMessage("ocpp16-in", ocppMessage);
                            });

                            Match match = Regex.Match(ocppMessage, MessageRegExp);
                            if (match != null && match.Groups != null && match.Groups.Count >= 3)
                            {
                                string messageTypeId = match.Groups[1].Value;
                                string uniqueId = match.Groups[2].Value;
                                string action = match.Groups[3].Value;
                                string jsonPaylod = match.Groups[4].Value;
                                logger.LogInformation("OCPPMiddleware.Receive16 => OCPP-Message: Type={0} / ID={1} / Action={2})", messageTypeId, uniqueId, action);

                                OCPPMessage msgIn = new OCPPMessage(messageTypeId, uniqueId, action, jsonPaylod);

                                // Send raw incoming messages to extensions
                                _ = Task.Run(() =>
                                {
                                    ProcessRawIncomingMessageSinks(chargePointStatus.Protocol, chargePointStatus.Id, msgIn);
                                });

                                if (msgIn.MessageType == "2")
                                {
                                    // Request from chargepoint to OCPP server
                                    OCPPMessage msgOut = controller16.ProcessRequest(msgIn);

                                    // Send OCPP message with optional logging/dump
                                    await SendOcpp16Message(msgOut, logger, chargePointStatus);
                                }
                                else if (msgIn.MessageType == "3" || msgIn.MessageType == "4")
                                {
                                    // Process answer from chargepoint
                                    if (_requestQueue.ContainsKey(msgIn.UniqueId))
                                    {
                                        controller16.ProcessAnswer(msgIn, _requestQueue[msgIn.UniqueId]);
                                        _requestQueue.Remove(msgIn.UniqueId);
                                    }
                                    else
                                    {
                                        logger.LogError("OCPPMiddleware.Receive16 => HttpContext from caller not found / Msg: {0}", ocppMessage);
                                    }
                                }
                                else
                                {
                                    // Unknown message type
                                    logger.LogError("OCPPMiddleware.Receive16 => Unknown message type: {0} / Msg: {1}", msgIn.MessageType, ocppMessage);
                                }
                            }
                            else
                            {
                                logger.LogWarning("OCPPMiddleware.Receive16 => Error in RegEx-Matching: Msg={0})", ocppMessage);
                            }
                        }
                    }
                    else
                    {
                        // max. allowed message size exceeded => close connection (DoS attack?)
                        logger.LogInformation("OCPPMiddleware.Receive16 => Allowed message size exceeded - close connection");
                        await chargePointStatus.WebSocket.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, string.Empty, CancellationToken.None);
                    }
                }
                else
                {
                    logger.LogInformation("OCPPMiddleware.Receive16 => WebSocket Closed: CloseStatus={0} / MessageType={1}", result?.CloseStatus, result?.MessageType);
                    await chargePointStatus.WebSocket.CloseOutputAsync((WebSocketCloseStatus)3001, string.Empty, CancellationToken.None);
                }
            }
            logger.LogInformation("OCPPMiddleware.Receive16 => Websocket closed: State={0} / CloseStatus={1}", chargePointStatus.WebSocket.State, chargePointStatus.WebSocket.CloseStatus);
            ChargePointStatus dummy;
            _chargePointStatusDict.Remove(chargePointStatus.Id, out dummy);
        }

        /// <summary>
        /// Waits for new OCPP V1.6 messages on the open websocket connection and delegates processing to a controller
        /// </summary>
        private async Task Reset16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus, dbContext);

            Messages_OCPP16.ResetRequest resetRequest = new Messages_OCPP16.ResetRequest();
            resetRequest.Type = Messages_OCPP16.ResetRequestType.Soft;
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
            await SendOcpp16Message(msgOut, logger, chargePointStatus);

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
        private async Task UnlockConnector16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus, dbContext);

            Messages_OCPP16.UnlockConnectorRequest unlockConnectorRequest = new Messages_OCPP16.UnlockConnectorRequest();
            unlockConnectorRequest.ConnectorId = 0;

            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                if (int.TryParse(urlConnectorId, out int iConnectorId))
                {
                    unlockConnectorRequest.ConnectorId = iConnectorId;
                }
            }
            logger.LogTrace("OCPPMiddleware.OCPP16 => UnlockConnector16: ChargePoint='{0}' / ConnectorId={1}", chargePointStatus.Id, unlockConnectorRequest.ConnectorId);

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
            await SendOcpp16Message(msgOut, logger, chargePointStatus);

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
        private async Task SetChargingProfile16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId, double power, string unit)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus, dbContext);

            // Parse connector id (int value)
            int connectorId = 0;
            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                int.TryParse(urlConnectorId, out connectorId);
            }

            Messages_OCPP16.SetChargingProfileRequest setChargingProfileRequest = new Messages_OCPP16.SetChargingProfileRequest();
            setChargingProfileRequest.ConnectorId = connectorId;
            setChargingProfileRequest.CsChargingProfiles = new Messages_OCPP16.CsChargingProfiles();
            // Default values
            setChargingProfileRequest.CsChargingProfiles.ChargingProfileId = 100;
            setChargingProfileRequest.CsChargingProfiles.StackLevel = 1;
            setChargingProfileRequest.CsChargingProfiles.ChargingProfilePurpose = CsChargingProfilesChargingProfilePurpose.TxDefaultProfile;
            setChargingProfileRequest.CsChargingProfiles.ChargingProfileKind = CsChargingProfilesChargingProfileKind.Absolute;
            setChargingProfileRequest.CsChargingProfiles.ValidFrom = DateTime.UtcNow;
            setChargingProfileRequest.CsChargingProfiles.ValidTo = DateTime.UtcNow.AddYears(1);
            setChargingProfileRequest.CsChargingProfiles.ChargingSchedule = new ChargingSchedule()
            {
                ChargingRateUnit = string.Equals(unit, "A", StringComparison.InvariantCultureIgnoreCase) ? ChargingScheduleChargingRateUnit.A : ChargingScheduleChargingRateUnit.W,
                ChargingSchedulePeriod = new List<ChargingSchedulePeriod>()
                {
                    new ChargingSchedulePeriod()
                    {
                        StartPeriod = 0,    // Start 0:00h
                        Limit = power,
                        NumberPhases = null
                    }
                }
            };

            logger.LogInformation ("OCPPMiddleware.OCPP16 => SetChargingProfile16: ChargePoint='{0}' / ConnectorId={1} / Power='{2}{3}'", chargePointStatus.Id, setChargingProfileRequest.ConnectorId, power, unit);

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
            await SendOcpp16Message(msgOut, logger, chargePointStatus);

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
        private async Task ClearChargingProfile16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus, dbContext);

            Messages_OCPP16.ClearChargingProfileRequest clearChargingProfileRequest = new Messages_OCPP16.ClearChargingProfileRequest();
            // Default values
            clearChargingProfileRequest.Id = 100;
            clearChargingProfileRequest.StackLevel = 1;
            clearChargingProfileRequest.ChargingProfilePurpose = ClearChargingProfileRequestChargingProfilePurpose.TxDefaultProfile;

            clearChargingProfileRequest.ConnectorId = 0;
            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                if (int.TryParse(urlConnectorId, out int iConnectorId))
                {
                    clearChargingProfileRequest.ConnectorId = iConnectorId;
                }
            }
            logger.LogTrace("OCPPMiddleware.OCPP16 => ClearChargingProfile16: ChargePoint='{0}' / ConnectorId={1}", chargePointStatus.Id, clearChargingProfileRequest.ConnectorId);

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
            await SendOcpp16Message(msgOut, logger, chargePointStatus);

            // Wait for asynchronous chargepoint response and processing
            string apiResult = await msgOut.TaskCompletionSource.Task;

            // 
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        private async Task SendOcpp16Message(OCPPMessage msg, ILogger logger, ChargePointStatus chargePointStatus)
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
            logger.LogTrace("OCPPMiddleware.OCPP16 => SendOcppMessage: {0}", ocppTextMessage);

            if (string.IsNullOrEmpty(ocppTextMessage))
            {
                // invalid message
                ocppTextMessage = string.Format("[{0},\"{1}\",\"{2}\",\"{3}\",{4}]", "4", string.Empty, Messages_OCPP16.ErrorCodes.ProtocolError, string.Empty, "{}");
            }

            // write message (async) to dump directory
            _ = Task.Run(() =>
            {
                DumpMessage("ocpp16-out", ocppTextMessage);
            });

            byte[] binaryMessage = UTF8Encoding.UTF8.GetBytes(ocppTextMessage);
            await chargePointStatus.WebSocket.SendAsync(new ArraySegment<byte>(binaryMessage, 0, binaryMessage.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
