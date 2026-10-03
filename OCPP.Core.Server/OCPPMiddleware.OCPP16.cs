/*
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
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OCPP.Core.Server.Messages_OCPP16;
using OCPP.Core.Server.Messages_Api;
using OCPP.Core.Database;

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {

        /// <summary>
        /// Creates a short, collision-resistant unique message ID.
        /// Some chargers (e.g. eNovates firmware) truncate the echoed OCPP UniqueId
        /// to a limited length, which breaks response correlation when a full 32-char
        /// GUID is used. This produces an 11-character ID from a 62-character
        /// alphanumeric alphabet (A-Z, a-z, 0-9), giving ~65 bits of entropy while
        /// staying within the length some firmware tolerates and remaining safe inside
        /// the OCPP JSON/WebSocket framing (no '+', '/', '=' characters).
        /// </summary>
        private static string NewShortUniqueId()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            const int length = 11;
            byte[] bytes = new byte[length];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            char[] result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[bytes[i] % chars.Length];
            }
            return new string(result);
        }

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

            try
            {
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
                                // reset memory stream for next message
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
                                        OCPPMessage msgOut = controller16.ProcessRequest(msgIn, this);

                                        // Send OCPP message with optional logging/dump
                                        await SendOcpp16Message(msgOut, logger, chargePointStatus);
                                    }
                                    else if (msgIn.MessageType == "3" || msgIn.MessageType == "4")
                                    {
                                        // Process answer from chargepoint
                                        if (chargePointStatus.PendingRequests.TryRemove(msgIn.UniqueId, out OCPPMessage msgRequest))
                                        {
                                            controller16.ProcessAnswer(msgIn, msgRequest);
                                        }
                                        else
                                        {
                                            logger.LogError("OCPPMiddleware.Receive16 => Request not found (late answer after timeout?) / Msg: {0}", ocppMessage);
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
                            await CloseOutputAsync(chargePointStatus, WebSocketCloseStatus.MessageTooBig);
                        }
                    }
                    else
                    {
                        logger.LogInformation("OCPPMiddleware.Receive16 => WebSocket Closed: CloseStatus={0} / MessageType={1}", result?.CloseStatus, result?.MessageType);
                        await CloseOutputAsync(chargePointStatus, (WebSocketCloseStatus)3001);
                    }
                }
            }
            finally
            {
                logger.LogInformation("OCPPMiddleware.Receive16 => Websocket closed: State={0} / CloseStatus={1}", chargePointStatus.WebSocket.State, chargePointStatus.WebSocket.CloseStatus);
                AbortPendingRequests(chargePointStatus);
                _chargePointStatusDict.TryRemove(chargePointStatus.Id, out _);
            }
        }

        /// <summary>
        /// Waits for new OCPP V1.6 messages on the open websocket connection and delegates processing to a controller
        /// </summary>
        private async Task Reset16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus, dbContext);

            logger.LogTrace("OCPPMiddleware.OCPP16 => Reset16: ChargePoint='{0}'", chargePointStatus.Id);

            Messages_OCPP16.ResetRequest resetRequest = new Messages_OCPP16.ResetRequest();
            resetRequest.Type = Messages_OCPP16.ResetRequestType.Soft;
            // Send request and wait (asynchronously) for the chargepoint response
            string apiResult = await SendRequestAndWait(chargePointStatus, "Reset", resetRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);

            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Reads variables (=configuration keys) from the chargepoint via GetConfiguration.
        /// OCPP 1.6 has no components => only entries without component are sent to the chargepoint.
        /// No entries => the chargepoint returns all configuration keys.
        /// </summary>
        private async Task GetVariables16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, ApiVariablesRequest apiRequest)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");

            bool fullList = (apiRequest.Variables.Count == 0);
            Dictionary<ApiVariableData, ApiVariableResult> localResults = new Dictionary<ApiVariableData, ApiVariableResult>();
            List<string> keys = new List<string>();
            foreach (ApiVariableData data in apiRequest.Variables)
            {
                ApiVariableResult localResult = CheckVariable16(data);
                if (localResult != null)
                {
                    localResults.Add(data, localResult);
                }
                else
                {
                    keys.Add(data.Variable.Name);
                }
            }
            logger.LogTrace("OCPPMiddleware.OCPP16 => GetVariables16: ChargePoint='{0}' / Keys='{1}'", chargePointStatus.Id, string.Join(",", keys));

            GetConfigurationResponse getConfigurationResponse = null;
            bool timeout = false;
            if (fullList || keys.Count > 0)
            {
                Messages_OCPP16.GetConfigurationRequest getConfigurationRequest = new Messages_OCPP16.GetConfigurationRequest();
                if (keys.Count > 0)
                {
                    getConfigurationRequest.Key = keys;
                }
                // else: no key => charger returns ALL configuration keys

                string ocppResult = await SendRequestAndWait(chargePointStatus, "GetConfiguration", getConfigurationRequest, logger, apiCallerContext.RequestAborted);
                if (ocppResult != null)
                {
                    getConfigurationResponse = JsonConvert.DeserializeObject<GetConfigurationResponse>(ocppResult);
                }
                else
                {
                    timeout = true;
                }
            }

            string apiResult;
            if (fullList && timeout)
            {
                apiResult = JsonConvert.SerializeObject(new ApiVariablesResponse() { Status = NoAnswerStatus(chargePointStatus) });
            }
            else
            {
                Dictionary<string, ConfigurationKey> configKeys = new Dictionary<string, ConfigurationKey>(StringComparer.OrdinalIgnoreCase);
                if (getConfigurationResponse?.ConfigurationKey != null)
                {
                    foreach (ConfigurationKey configKey in getConfigurationResponse.ConfigurationKey)
                    {
                        configKeys[configKey.Key] = configKey;
                    }
                }

                ApiVariablesResponse apiResponse = new ApiVariablesResponse();
                if (fullList)
                {
                    foreach (ConfigurationKey configKey in configKeys.Values)
                    {
                        apiResponse.Variables.Add(CreateResult16(configKey));
                    }
                }
                else
                {
                    // results in the order of the request
                    foreach (ApiVariableData data in apiRequest.Variables)
                    {
                        if (localResults.TryGetValue(data, out ApiVariableResult localResult))
                        {
                            apiResponse.Variables.Add(localResult);
                        }
                        else if (timeout)
                        {
                            apiResponse.Variables.Add(data.CreateResult(NoAnswerStatus(chargePointStatus)));
                        }
                        else if (configKeys.TryGetValue(data.Variable.Name, out ConfigurationKey configKey))
                        {
                            apiResponse.Variables.Add(CreateResult16(configKey));
                        }
                        else
                        {
                            // listed in "unknownKey" (or missing in the response)
                            apiResponse.Variables.Add(data.CreateResult(ApiVariableStatus.UnknownVariable));
                        }
                    }
                }
                apiResult = JsonConvert.SerializeObject(apiResponse);
            }

            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Sets variables (=configuration keys) on the chargepoint.
        /// OCPP 1.6 only allows one key per ChangeConfiguration => one message per entry.
        /// </summary>
        private async Task SetVariables16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, ApiVariablesRequest apiRequest)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");

            ApiVariablesResponse apiResponse = new ApiVariablesResponse();
            bool timeout = false;
            foreach (ApiVariableData data in apiRequest.Variables)
            {
                ApiVariableResult result = CheckVariable16(data);
                if (result == null)
                {
                    if (timeout)
                    {
                        // Chargepoint didn't answer the previous request => don't send more requests
                        result = data.CreateResult(NoAnswerStatus(chargePointStatus));
                    }
                    else
                    {
                        logger.LogInformation("OCPPMiddleware.OCPP16 => SetVariables16: ChargePoint='{0}' / Key='{1}' / Value='{2}'", chargePointStatus.Id, data.Variable.Name, data.Value);

                        Messages_OCPP16.ChangeConfigurationRequest changeConfigurationRequest = new Messages_OCPP16.ChangeConfigurationRequest();
                        changeConfigurationRequest.Key = data.Variable.Name;
                        changeConfigurationRequest.Value = data.Value;

                        string ocppResult = await SendRequestAndWait(chargePointStatus, "ChangeConfiguration", changeConfigurationRequest, logger, apiCallerContext.RequestAborted);
                        if (ocppResult != null)
                        {
                            ChangeConfigurationResponse changeConfigurationResponse = JsonConvert.DeserializeObject<ChangeConfigurationResponse>(ocppResult);
                            string status = changeConfigurationResponse.Status switch
                            {
                                ChangeConfigurationResponseStatus.Accepted => ApiVariableStatus.Accepted,
                                ChangeConfigurationResponseStatus.RebootRequired => ApiVariableStatus.RebootRequired,
                                ChangeConfigurationResponseStatus.NotSupported => ApiVariableStatus.UnknownVariable,
                                _ => ApiVariableStatus.Rejected
                            };
                            result = data.CreateResult(status);
                        }
                        else
                        {
                            timeout = true;
                            result = data.CreateResult(NoAnswerStatus(chargePointStatus));
                        }
                    }
                }
                apiResponse.Variables.Add(result);
            }

            string apiResult = JsonConvert.SerializeObject(apiResponse);
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Checks if a variable can be mapped to an OCPP 1.6 configuration key.
        /// Returns a (negative) result if not - otherwise null.
        /// </summary>
        private static ApiVariableResult CheckVariable16(ApiVariableData data)
        {
            if (data.HasComponent)
            {
                return data.CreateResult(ApiVariableStatus.UnknownComponent, "OCPP 1.6 has no components");
            }
            if (!string.IsNullOrEmpty(data.Variable.Instance))
            {
                return data.CreateResult(ApiVariableStatus.UnknownVariable, "OCPP 1.6 has no variable instances");
            }
            if (!data.IsActualAttribute)
            {
                return data.CreateResult(ApiVariableStatus.NotSupportedAttributeType, "OCPP 1.6 only supports attribute type 'Actual'");
            }
            return null;
        }

        /// <summary>
        /// Creates an API result from an OCPP 1.6 configuration key
        /// </summary>
        private static ApiVariableResult CreateResult16(ConfigurationKey configKey)
        {
            return new ApiVariableResult()
            {
                Variable = new ApiVariable() { Name = configKey.Key },
                Value = configKey.Value,
                Status = ApiVariableStatus.Accepted,
                Mutability = configKey.Readonly ? ApiMutability.ReadOnly : ApiMutability.ReadWrite
            };
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

            // Send request and wait (asynchronously) for the chargepoint response
            string apiResult = await SendRequestAndWait(chargePointStatus, "UnlockConnector", unlockConnectorRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);

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

            // Send request and wait (asynchronously) for the chargepoint response
            string apiResult = await SendRequestAndWait(chargePointStatus, "SetChargingProfile", setChargingProfileRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);

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

            // Send request and wait (asynchronously) for the chargepoint response
            string apiResult = await SendRequestAndWait(chargePointStatus, "ClearChargingProfile", clearChargingProfileRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);

            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Send a RemoteStartTransaction-Request to the chargepoint
        /// </summary>
        private async Task RemoteStartTransaction16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId, string idTag)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus, dbContext);

            // Parse connector id (int value)
            int connectorId = 0;
            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                int.TryParse(urlConnectorId, out connectorId);
            }

            string apiResult = string.Empty;

            // Use Authorize logic to check idTag
            bool denyConcurrentTx = _configuration.GetValue<bool>("DenyConcurrentTx", false);
            IdTagInfo idTagInfo = controller16.InternalAuthorize(idTag, this, connectorId, Extensions.Interfaces.AuthAction.StartTransaction, string.Empty, string.Empty, denyConcurrentTx);
            if (idTagInfo?.Status == IdTagInfoStatus.Accepted)
            {
                // Valid idTag => send request to charge point

                Messages_OCPP16.RemoteStartTransactionRequest remoteStartTransactionRequest = new Messages_OCPP16.RemoteStartTransactionRequest();
                remoteStartTransactionRequest.ConnectorId = connectorId;
                remoteStartTransactionRequest.IdTag = idTag;

                logger.LogInformation("OCPPMiddleware.OCPP16 => RemoteStartTransaction16: ChargePoint='{0}' / ConnectorId={1} / idTag='{2}'", chargePointStatus.Id, remoteStartTransactionRequest.ConnectorId, idTag);

                // Send request and wait (asynchronously) for the chargepoint response
                apiResult = await SendRequestAndWait(chargePointStatus, "RemoteStartTransaction", remoteStartTransactionRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);
            }
            else
            {
                // Invalid or blocked idTag => return status "Rejected"
                apiResult = "{\"status\": \"Rejected\"}";
            }

            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Send a RemoteStopTransaction-Request to the chargepoint
        /// </summary>
        private async Task RemoteStopTransaction16(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId, int transactionId)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP16");
            ControllerOCPP16 controller16 = new ControllerOCPP16(_configuration, _logFactory, chargePointStatus, dbContext);

            // Parse connector id (int value)
            int connectorId = 0;
            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                int.TryParse(urlConnectorId, out connectorId);
            }

            Messages_OCPP16.RemoteStopTransactionRequest remoteStopTransactionRequest = new Messages_OCPP16.RemoteStopTransactionRequest();
            remoteStopTransactionRequest.TransactionId = transactionId;

            logger.LogInformation("OCPPMiddleware.OCPP16 => RemoteStopTransaction16: ChargePoint='{0}' / ConnectorId={1} / TransactionId='{2}'", chargePointStatus.Id, connectorId, transactionId);

            // Send request and wait (asynchronously) for the chargepoint response
            string apiResult = await SendRequestAndWait(chargePointStatus, "RemoteStopTransaction", remoteStopTransactionRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);

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
            // only one send operation at a time on the WebSocket
            await chargePointStatus.SendLock.WaitAsync();
            try
            {
                await chargePointStatus.WebSocket.SendAsync(new ArraySegment<byte>(binaryMessage, 0, binaryMessage.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                chargePointStatus.SendLock.Release();
            }
        }
    }
}
