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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OCPP.Core.Server.Messages_OCPP20;
using OCPP.Core.Server.Messages_Api;
using OCPP.Core.Database;

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {
        /// <summary>
        /// Waits for new OCPP V2.0 messages on the open websocket connection and delegates processing to a controller
        /// </summary>
        private async Task Receive20(ChargePointStatus chargePointStatus, HttpContext httpContext, OCPPCoreContext dbContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");
            ControllerOCPP20 controller20 = new ControllerOCPP20(_configuration, _logFactory, chargePointStatus, dbContext);

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
                        logger.LogTrace("OCPPMiddleware.Receive20 => Receiving segment: {0} bytes (EndOfMessage={1} / MsgType={2})", result.Count, result.EndOfMessage, result.MessageType);
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
                                    DumpMessage("ocpp201-in", ocppMessage);
                                });

                                Match match = Regex.Match(ocppMessage, MessageRegExp);
                                if (match != null && match.Groups != null && match.Groups.Count >= 3)
                                {
                                    string messageTypeId = match.Groups[1].Value;
                                    string uniqueId = match.Groups[2].Value;
                                    string action = match.Groups[3].Value;
                                    string jsonPaylod = match.Groups[4].Value;
                                    logger.LogInformation("OCPPMiddleware.Receive20 => OCPP-Message: Type={0} / ID={1} / Action={2})", messageTypeId, uniqueId, action);

                                    OCPPMessage msgIn = new OCPPMessage(messageTypeId, uniqueId, action, jsonPaylod);

                                    // Send raw incoming messages to extensions
                                    _ = Task.Run(() =>
                                    {
                                        ProcessRawIncomingMessageSinks(chargePointStatus.Protocol, chargePointStatus.Id, msgIn);
                                    });

                                    if (msgIn.MessageType == "2")
                                    {
                                        // Request from chargepoint to OCPP server
                                        OCPPMessage msgOut = controller20.ProcessRequest(msgIn, this);

                                        // Send OCPP message with optional logging/dump
                                        await SendOcpp20Message(msgOut, logger, chargePointStatus);
                                    }
                                    else if (msgIn.MessageType == "3" || msgIn.MessageType == "4")
                                    {
                                        // Process answer from chargepoint
                                        if (chargePointStatus.PendingRequests.TryRemove(msgIn.UniqueId, out OCPPMessage msgRequest))
                                        {
                                            controller20.ProcessAnswer(msgIn, msgRequest);
                                        }
                                        else
                                        {
                                            logger.LogError("OCPPMiddleware.Receive20 => Request not found (late answer after timeout?) / Msg: {0}", ocppMessage);
                                        }
                                    }
                                    else
                                    {
                                        // Unknown message type
                                        logger.LogError("OCPPMiddleware.Receive20 => Unknown message type: {0} / Msg: {1}", msgIn.MessageType, ocppMessage);
                                    }
                                }
                                else
                                {
                                    logger.LogWarning("OCPPMiddleware.Receive20 => Error in RegEx-Matching: Msg={0})", ocppMessage);
                                }
                            }
                        }
                        else
                        {
                            // max. allowed message size exceeded => close connection (DoS attack?)
                            logger.LogInformation("OCPPMiddleware.Receive20 => Allowed message size exceeded - close connection");
                            await CloseOutputAsync(chargePointStatus, WebSocketCloseStatus.MessageTooBig);
                        }
                    }
                    else
                    {
                        logger.LogInformation("OCPPMiddleware.Receive20 => Receive: unexpected result: CloseStatus={0} / MessageType={1}", result?.CloseStatus, result?.MessageType);
                        await CloseOutputAsync(chargePointStatus, (WebSocketCloseStatus)3001);
                    }
                }
            }
            finally
            {
                logger.LogInformation("OCPPMiddleware.Receive20 => Websocket closed: State={0} / CloseStatus={1}", chargePointStatus.WebSocket.State, chargePointStatus.WebSocket.CloseStatus);
                AbortPendingRequests(chargePointStatus);
                _chargePointStatusDict.TryRemove(chargePointStatus.Id, out _);
            }
        }

        /// <summary>
        /// Sends a (Soft-)Reset to the chargepoint
        /// </summary>
        private async Task Reset20(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");
            ControllerOCPP20 controller20 = new ControllerOCPP20(_configuration, _logFactory, chargePointStatus, dbContext);

            logger.LogTrace("OCPPMiddleware.OCPP20 => Reset20: ChargePoint='{0}'", chargePointStatus.Id);

            Messages_OCPP20.ResetRequest resetRequest = new Messages_OCPP20.ResetRequest();
            resetRequest.Type = Messages_OCPP20.ResetEnumType.OnIdle;
            resetRequest.CustomData = new CustomDataType();
            resetRequest.CustomData.VendorId = ControllerOCPP20.VendorId;

            // Send request and wait (asynchronously) for the chargepoint response
            string apiResult = await SendRequestAndWait(chargePointStatus, "Reset", resetRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);

            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Sends a Unlock-Request to the chargepoint
        /// </summary>
        private async Task UnlockConnector20(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");
            ControllerOCPP20 controller20 = new ControllerOCPP20(_configuration, _logFactory, chargePointStatus, dbContext);

            Messages_OCPP20.UnlockConnectorRequest unlockConnectorRequest = new Messages_OCPP20.UnlockConnectorRequest();
            unlockConnectorRequest.EvseId = 0;
            unlockConnectorRequest.CustomData = new CustomDataType();
            unlockConnectorRequest.CustomData.VendorId = ControllerOCPP20.VendorId;

            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                if (int.TryParse(urlConnectorId, out int iConnectorId))
                {
                    unlockConnectorRequest.EvseId = iConnectorId;
                }
            }
            logger.LogTrace("OCPPMiddleware.OCPP20 => UnlockConnector20: ChargePoint='{0}' / EvseId={1}", chargePointStatus.Id, unlockConnectorRequest.EvseId);


            // Send request and wait (asynchronously) for the chargepoint response
            string apiResult = await SendRequestAndWait(chargePointStatus, "UnlockConnector", unlockConnectorRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);

            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Sends a SetChargingProfile-Request to the chargepoint
        /// </summary>
        private async Task SetChargingProfile20(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId, double power, string unit)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");
            ControllerOCPP20 controller20 = new ControllerOCPP20(_configuration, _logFactory, chargePointStatus, dbContext);

            // Parse connector id (int value)
            int connectorId = 0;
            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                int.TryParse(urlConnectorId, out connectorId);
            }

            Messages_OCPP20.SetChargingProfileRequest setChargingProfileRequest = new Messages_OCPP20.SetChargingProfileRequest();
            setChargingProfileRequest.EvseId = connectorId;
            setChargingProfileRequest.ChargingProfile = new Messages_OCPP20.ChargingProfileType();
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

            logger.LogInformation("OCPPMiddleware.OCPP20 => SetChargingProfile20: ChargePoint='{0}' / ConnectorId={1} / Power='{2}{3}'", chargePointStatus.Id, setChargingProfileRequest.EvseId, power, unit);

            // Send request and wait (asynchronously) for the chargepoint response
            string apiResult = await SendRequestAndWait(chargePointStatus, "SetChargingProfile", setChargingProfileRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);

            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Sends a ClearChargingProfile-Request to the chargepoint
        /// </summary>
        private async Task ClearChargingProfile20(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");
            ControllerOCPP20 controller20 = new ControllerOCPP20(_configuration, _logFactory, chargePointStatus, dbContext);

            Messages_OCPP20.ClearChargingProfileRequest clearChargingProfileRequest = new Messages_OCPP20.ClearChargingProfileRequest();
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
            logger.LogTrace("OCPPMiddleware.OCPP20 => ClearChargingProfile20: ChargePoint='{0}' / ConnectorId={1}", chargePointStatus.Id, clearChargingProfileRequest.ChargingProfileCriteria.EvseId);

            // Send request and wait (asynchronously) for the chargepoint response
            string apiResult = await SendRequestAndWait(chargePointStatus, "ClearChargingProfile", clearChargingProfileRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);

            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Send a RequestStartTransaction-Request to the chargepoint
        /// </summary>
        private async Task RequestStartTransaction20(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId, string idTag)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");
            ControllerOCPP20 controller20 = new ControllerOCPP20(_configuration, _logFactory, chargePointStatus, dbContext);

            // Parse connector id (int value)
            int connectorId = 0;
            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                int.TryParse(urlConnectorId, out connectorId);
            }

            string apiResult = string.Empty;

            // Use Authorize logic to check idTag
            IdTokenInfoType idTokenInfo = controller20.InternalAuthorize(idTag, this);
            if (idTokenInfo.Status == AuthorizationStatusEnumType.Accepted)
            {
                // Valid idTag => send request to charge point

                Messages_OCPP20.RequestStartTransactionRequest requestStartTransactionRequest = new Messages_OCPP20.RequestStartTransactionRequest();
                requestStartTransactionRequest.EvseId = connectorId;
                requestStartTransactionRequest.IdToken = new IdTokenType();
                requestStartTransactionRequest.IdToken.Type = IdTokenEnumType.ISO14443;
                requestStartTransactionRequest.IdToken.IdToken = idTag;

                logger.LogInformation("OCPPMiddleware.OCPP20 => RequestStartTransaction20: ChargePoint='{0}' / ConnectorId={1} / idTag='{2}'", chargePointStatus.Id, connectorId, idTag);

                // Send request and wait (asynchronously) for the chargepoint response
                apiResult = await SendRequestAndWait(chargePointStatus, "RequestStartTransaction", requestStartTransactionRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);
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
        /// Send a RequestStopTransaction-Request to the chargepoint
        /// </summary>
        private async Task RequestStopTransaction20(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, string urlConnectorId, string transactionId)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");
            ControllerOCPP20 controller20 = new ControllerOCPP20(_configuration, _logFactory, chargePointStatus, dbContext);

            // Parse connector id (int value)
            int connectorId = 0;
            if (!string.IsNullOrEmpty(urlConnectorId))
            {
                int.TryParse(urlConnectorId, out connectorId);
            }

            Messages_OCPP20.RequestStopTransactionRequest requestStopTransactionRequest = new Messages_OCPP20.RequestStopTransactionRequest();
            requestStopTransactionRequest.TransactionId = transactionId;

            logger.LogInformation("OCPPMiddleware.OCPP20 => RequestStopTransaction20: ChargePoint='{0}' / ConnectorId={1} / TransactionId='{2}'", chargePointStatus.Id, connectorId, transactionId);

            // Send request and wait (asynchronously) for the chargepoint response
            string apiResult = await SendRequestAndWait(chargePointStatus, "RequestStopTransaction", requestStopTransactionRequest, logger, apiCallerContext.RequestAborted) ?? NoAnswerResult(chargePointStatus);

            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Sends a GetVariables-Request to the chargepoint
        /// </summary>
        /// <summary>
        /// Reads all variables via GetBaseReport and collects the NotifyReport messages
        /// </summary>
        private async Task GetReport20(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, string reportBaseParam)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");

            ReportBaseEnumType reportBase = ReportBaseEnumType.ConfigurationInventory;
            if (!string.IsNullOrEmpty(reportBaseParam) &&
                (char.IsDigit(reportBaseParam[0]) || !Enum.TryParse<ReportBaseEnumType>(reportBaseParam, true, out reportBase)))
            {
                logger.LogError("OCPPMiddleware.OCPP20 => GetReport20: Invalid report base '{0}'", reportBaseParam);
                apiCallerContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            // Register the report before sending the request => no early NotifyReport gets lost
            int requestId = chargePointStatus.NewReportRequestId();
            PendingReport pendingReport = new PendingReport(requestId, _configuration.GetValue<int>("ReportMaxItems", 10000));
            chargePointStatus.PendingReports.TryAdd(requestId, pendingReport);
            try
            {
                logger.LogInformation("OCPPMiddleware.OCPP20 => GetReport20: ChargePoint='{0}' / RequestId={1} / ReportBase={2}", chargePointStatus.Id, requestId, reportBase);

                Messages_OCPP20.GetBaseReportRequest getBaseReportRequest = new Messages_OCPP20.GetBaseReportRequest();
                getBaseReportRequest.RequestId = requestId;
                getBaseReportRequest.ReportBase = reportBase;
                getBaseReportRequest.CustomData = new CustomDataType();
                getBaseReportRequest.CustomData.VendorId = ControllerOCPP20.VendorId;

                ApiVariablesResponse apiResponse;
                string ocppResult = await SendRequestAndWait(chargePointStatus, "GetBaseReport", getBaseReportRequest, logger, apiCallerContext.RequestAborted);
                if (ocppResult == null)
                {
                    apiResponse = new ApiVariablesResponse() { Status = NoAnswerStatus(chargePointStatus) };
                }
                else
                {
                    Messages_OCPP20.GetBaseReportResponse getBaseReportResponse = JsonConvert.DeserializeObject<Messages_OCPP20.GetBaseReportResponse>(ocppResult);
                    switch (getBaseReportResponse.Status)
                    {
                        case GenericDeviceModelStatusEnumType.Accepted:
                            string abortStatus = await WaitForReport(pendingReport, chargePointStatus, logger, apiCallerContext.RequestAborted);
                            apiResponse = pendingReport.CreateResponse(abortStatus);
                            break;
                        case GenericDeviceModelStatusEnumType.EmptyResultSet:
                            apiResponse = new ApiVariablesResponse();
                            break;
                        default:
                            apiResponse = new ApiVariablesResponse()
                            {
                                Status = getBaseReportResponse.Status.ToString(),
                                StatusInfo = ToApiStatusInfo20(getBaseReportResponse.StatusInfo)
                            };
                            break;
                    }
                }
                logger.LogInformation("OCPPMiddleware.OCPP20 => GetReport20: ChargePoint='{0}' / RequestId={1} => Status={2} / Variables={3}", chargePointStatus.Id, requestId, apiResponse.Status ?? "Complete", apiResponse.Variables.Count);

                string apiResult = JsonConvert.SerializeObject(apiResponse);
                apiCallerContext.Response.StatusCode = 200;
                apiCallerContext.Response.ContentType = "application/json";
                await apiCallerContext.Response.WriteAsync(apiResult);
            }
            finally
            {
                chargePointStatus.PendingReports.TryRemove(requestId, out _);
            }
        }

        private async Task GetVariables20(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, ApiVariablesRequest apiRequest)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");

            if (apiRequest.Variables.Count == 0)
            {
                // No variables => read all variables (report)
                await GetReport20(chargePointStatus, apiCallerContext, apiRequest.ReportBase);
                return;
            }

            ApiVariablesResponse apiResponse = new ApiVariablesResponse();
            List<ApiVariableData> sentVariables = new List<ApiVariableData>();
            Messages_OCPP20.GetVariablesRequest getVariablesRequest = new Messages_OCPP20.GetVariablesRequest();
            getVariablesRequest.CustomData = new CustomDataType();
            getVariablesRequest.CustomData.VendorId = ControllerOCPP20.VendorId;
            foreach (ApiVariableData data in apiRequest.Variables)
            {
                ApiVariableResult localResult = CheckVariable20(data, out AttributeEnumType? attributeType);
                if (localResult != null)
                {
                    apiResponse.Variables.Add(localResult);
                }
                else
                {
                    getVariablesRequest.GetVariableData.Add(new GetVariableDataType()
                    {
                        AttributeType = attributeType,
                        Component = ToComponentType20(data.Component),
                        Variable = ToVariableType20(data.Variable)
                    });
                    sentVariables.Add(data);
                }
            }
            logger.LogTrace("OCPPMiddleware.OCPP20 => GetVariables20: ChargePoint='{0}' / Variables='{1}'", chargePointStatus.Id, string.Join(",", sentVariables));

            if (sentVariables.Count > 0)
            {
                string ocppResult = await SendRequestAndWait(chargePointStatus, "GetVariables", getVariablesRequest, logger, apiCallerContext.RequestAborted);
                if (ocppResult != null)
                {
                    Messages_OCPP20.GetVariablesResponse getVariablesResponse = JsonConvert.DeserializeObject<Messages_OCPP20.GetVariablesResponse>(ocppResult);
                    foreach (GetVariableResultType result in getVariablesResponse.GetVariableResult)
                    {
                        apiResponse.Variables.Add(new ApiVariableResult()
                        {
                            Component = ToApiComponent20(result.Component),
                            Variable = ToApiVariable20(result.Variable),
                            AttributeType = result.AttributeType?.ToString(),
                            Value = result.AttributeValue,
                            Status = result.AttributeStatus.ToString(),
                            StatusInfo = ToApiStatusInfo20(result.AttributeStatusInfo)
                        });
                    }
                }
                else
                {
                    foreach (ApiVariableData data in sentVariables)
                    {
                        apiResponse.Variables.Add(data.CreateResult(NoAnswerStatus(chargePointStatus)));
                    }
                }
            }

            string apiResult = JsonConvert.SerializeObject(apiResponse);
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Sends a SetVariables-Request to the chargepoint
        /// </summary>
        private async Task SetVariables20(ChargePointStatus chargePointStatus, HttpContext apiCallerContext, OCPPCoreContext dbContext, ApiVariablesRequest apiRequest)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");

            ApiVariablesResponse apiResponse = new ApiVariablesResponse();
            List<ApiVariableData> sentVariables = new List<ApiVariableData>();
            Messages_OCPP20.SetVariablesRequest setVariablesRequest = new Messages_OCPP20.SetVariablesRequest();
            setVariablesRequest.CustomData = new CustomDataType();
            setVariablesRequest.CustomData.VendorId = ControllerOCPP20.VendorId;
            foreach (ApiVariableData data in apiRequest.Variables)
            {
                ApiVariableResult localResult = CheckVariable20(data, out AttributeEnumType? attributeType);
                if (localResult != null)
                {
                    apiResponse.Variables.Add(localResult);
                }
                else
                {
                    setVariablesRequest.SetVariableData.Add(new SetVariableDataType()
                    {
                        AttributeType = attributeType,
                        AttributeValue = data.Value,
                        Component = ToComponentType20(data.Component),
                        Variable = ToVariableType20(data.Variable)
                    });
                    sentVariables.Add(data);
                }
            }
            logger.LogInformation("OCPPMiddleware.OCPP20 => SetVariables20: ChargePoint='{0}' / Variables='{1}'", chargePointStatus.Id, string.Join(",", sentVariables.Select(v => $"{v}={v.Value}")));

            if (sentVariables.Count > 0)
            {
                string ocppResult = await SendRequestAndWait(chargePointStatus, "SetVariables", setVariablesRequest, logger, apiCallerContext.RequestAborted);
                if (ocppResult != null)
                {
                    Messages_OCPP20.SetVariablesResponse setVariablesResponse = JsonConvert.DeserializeObject<Messages_OCPP20.SetVariablesResponse>(ocppResult);
                    foreach (SetVariableResultType result in setVariablesResponse.SetVariableResult)
                    {
                        apiResponse.Variables.Add(new ApiVariableResult()
                        {
                            Component = ToApiComponent20(result.Component),
                            Variable = ToApiVariable20(result.Variable),
                            AttributeType = result.AttributeType?.ToString(),
                            Status = result.AttributeStatus.ToString(),
                            StatusInfo = ToApiStatusInfo20(result.AttributeStatusInfo)
                        });
                    }
                }
                else
                {
                    foreach (ApiVariableData data in sentVariables)
                    {
                        apiResponse.Variables.Add(data.CreateResult(NoAnswerStatus(chargePointStatus)));
                    }
                }
            }

            string apiResult = JsonConvert.SerializeObject(apiResponse);
            apiCallerContext.Response.StatusCode = 200;
            apiCallerContext.Response.ContentType = "application/json";
            await apiCallerContext.Response.WriteAsync(apiResult);
        }

        /// <summary>
        /// Checks if a variable can be sent to an OCPP 2.0 chargepoint.
        /// Returns a (negative) result if not - otherwise null.
        /// </summary>
        private static ApiVariableResult CheckVariable20(ApiVariableData data, out AttributeEnumType? attributeType)
        {
            attributeType = null;
            if (!data.HasComponent)
            {
                return data.CreateResult(ApiVariableStatus.UnknownComponent, "OCPP 2.0 requires a component");
            }
            if (!string.IsNullOrEmpty(data.AttributeType))
            {
                if (char.IsDigit(data.AttributeType[0]) ||
                    !Enum.TryParse<AttributeEnumType>(data.AttributeType, true, out AttributeEnumType parsedType))
                {
                    return data.CreateResult(ApiVariableStatus.NotSupportedAttributeType, $"Unknown attribute type '{data.AttributeType}'");
                }
                attributeType = parsedType;
            }
            return null;
        }

        private static ComponentType ToComponentType20(ApiComponent component)
        {
            return new ComponentType()
            {
                Name = component.Name,
                Instance = string.IsNullOrEmpty(component.Instance) ? null : component.Instance,
                Evse = (component.Evse == null) ? null : new EVSEType() { Id = component.Evse.Id, ConnectorId = component.Evse.ConnectorId }
            };
        }

        private static VariableType ToVariableType20(ApiVariable variable)
        {
            return new VariableType()
            {
                Name = variable.Name,
                Instance = string.IsNullOrEmpty(variable.Instance) ? null : variable.Instance
            };
        }

        private static ApiComponent ToApiComponent20(ComponentType component)
        {
            if (component == null) return null;
            return new ApiComponent()
            {
                Name = component.Name,
                Instance = component.Instance,
                Evse = (component.Evse == null) ? null : new ApiEvse() { Id = component.Evse.Id, ConnectorId = component.Evse.ConnectorId }
            };
        }

        private static ApiVariable ToApiVariable20(VariableType variable)
        {
            if (variable == null) return null;
            return new ApiVariable() { Name = variable.Name, Instance = variable.Instance };
        }

        private static string ToApiStatusInfo20(StatusInfoType statusInfo)
        {
            if (statusInfo == null) return null;
            return string.IsNullOrEmpty(statusInfo.AdditionalInfo) ? statusInfo.ReasonCode : $"{statusInfo.ReasonCode}: {statusInfo.AdditionalInfo}";
        }

        private async Task SendOcpp20Message(OCPPMessage msg, ILogger logger, ChargePointStatus chargePointStatus)
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
            logger.LogTrace("OCPPMiddleware.OCPP20 => SendOcppMessage: {0}", ocppTextMessage);

            if (string.IsNullOrEmpty(ocppTextMessage))
            {
                // invalid message
                ocppTextMessage = string.Format("[{0},\"{1}\",\"{2}\",\"{3}\",{4}]", "4", string.Empty, Messages_OCPP20.ErrorCodes.ProtocolError, string.Empty, "{}");
            }

            // write message (async) to dump directory
            _ = Task.Run(() =>
            {
                DumpMessage("ocpp201-out", ocppTextMessage);
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
