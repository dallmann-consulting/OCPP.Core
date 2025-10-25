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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {
        // Supported OCPP protocols (in order)
        private const string Protocol_OCPP16 = "ocpp1.6";
        private const string Protocol_OCPP201 = "ocpp2.0.1";
        private const string Protocol_OCPP21 = "ocpp2.1";
        private static readonly string[] SupportedProtocols = { Protocol_OCPP16, Protocol_OCPP201, Protocol_OCPP21 };

        // RegExp for splitting ocpp message parts
        // ^\[\s*(\d)\s*,\s*\"([^"]*)\"\s*,(?:\s*\"(\w*)\"\s*,)?\s*(.*)\s*\]$
        // Third block is optional, because responses don't have an action
        private static string MessageRegExp = "^\\[\\s*(\\d+)\\s*,\\s*\"([^\"]+)\"\\s*,\\s*(?:\\s*\"(\\w+)\"\\s*,)?\\s*(\\{[^}]+\\}|\\{[\\s\\S]*?\\})\\s*\\]$";

        // Timeout in ms for waiting for responses from charger (for communication scheme server => charger)
        private int TimoutWaitForCharger = 60 * 1000;   // 60 seconds


        private readonly RequestDelegate _next;
        private readonly ILoggerFactory _logFactory;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        // Dictionary with status objects for each charge point
        private static Dictionary<string, ChargePointStatus> _chargePointStatusDict = new Dictionary<string, ChargePointStatus>();

        // Dictionary for processing asynchronous API calls
        private Dictionary<string, OCPPMessage> _requestQueue = new Dictionary<string, OCPPMessage>();

        public OCPPMiddleware(RequestDelegate next, ILoggerFactory logFactory, IConfiguration configuration)
        {
            _next = next;
            _logFactory = logFactory;
            _configuration = configuration;

            _logger = logFactory.CreateLogger("OCPPMiddleware");

            LoadExtensions();
        }

        public async Task Invoke(HttpContext context, OCPPCoreContext dbContext)
        {
            _logger.LogTrace("OCPPMiddleware => Websocket request: Path='{0}'", context.Request.Path);

            ChargePointStatus chargePointStatus = null;

            if (context.Request.Path.StartsWithSegments("/OCPP"))
            {
                string chargepointIdentifier;
                string[] parts = context.Request.Path.Value.Split('/');
                if (string.IsNullOrWhiteSpace(parts[parts.Length - 1]))
                {
                    // (Last part - 1) is chargepoint identifier
                    chargepointIdentifier = parts[parts.Length - 2];
                }
                else
                {
                    // Last part is chargepoint identifier
                    chargepointIdentifier = parts[parts.Length - 1];
                }
                _logger.LogInformation("OCPPMiddleware => Connection request with chargepoint identifier = '{0}'", chargepointIdentifier);

                // Known chargepoint?
                if (!string.IsNullOrWhiteSpace(chargepointIdentifier))
                {
                    ChargePoint chargePoint = dbContext.Find<ChargePoint>(chargepointIdentifier);
                    if (chargePoint != null)
                    {
                        _logger.LogInformation("OCPPMiddleware => SUCCESS: Found chargepoint with identifier={0}", chargePoint.ChargePointId);

                        // Check optional chargepoint authentication
                        if (!string.IsNullOrWhiteSpace(chargePoint.Username))
                        {
                            // Chargepoint MUST send basic authentication header

                            bool basicAuthSuccess = false;
                            string authHeader = context.Request.Headers["Authorization"];
                            if (!string.IsNullOrEmpty(authHeader))
                            {
                                string[] cred = System.Text.ASCIIEncoding.ASCII.GetString(Convert.FromBase64String(authHeader.Substring(6))).Split(':');
                                if (cred.Length == 2 && chargePoint.Username == cred[0] && chargePoint.Password == cred[1])
                                {
                                    // Authentication match => OK
                                    _logger.LogInformation("OCPPMiddleware => SUCCESS: Basic authentication for chargepoint '{0}' match", chargePoint.ChargePointId);
                                    basicAuthSuccess = true;
                                }
                                else
                                {
                                    // Authentication does NOT match => Failure
                                    _logger.LogWarning("OCPPMiddleware => FAILURE: Basic authentication for chargepoint '{0}' does NOT match", chargePoint.ChargePointId);
                                }
                            }
                            if (basicAuthSuccess == false)
                            {
                                context.Response.Headers.Append("WWW-Authenticate", "Basic realm=\"OCPP.Core\"");
                                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                                return;
                            }

                        }
                        else if (!string.IsNullOrWhiteSpace(chargePoint.ClientCertThumb))
                        {
                            // Chargepoint MUST send basic authentication header

                            bool certAuthSuccess = false;
                            X509Certificate2 clientCert = context.Connection.ClientCertificate;
                            if (clientCert != null)
                            {
                                if (clientCert.Thumbprint.Equals(chargePoint.ClientCertThumb, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    // Authentication match => OK
                                    _logger.LogInformation("OCPPMiddleware => SUCCESS: Certificate authentication for chargepoint '{0}' match", chargePoint.ChargePointId);
                                    certAuthSuccess = true;
                                }
                                else
                                {
                                    // Authentication does NOT match => Failure
                                    _logger.LogWarning("OCPPMiddleware => FAILURE: Certificate authentication for chargepoint '{0}' does NOT match", chargePoint.ChargePointId);
                                }
                            }
                            if (certAuthSuccess == false)
                            {
                                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                                return;
                            }
                        }
                        else
                        {
                            _logger.LogInformation("OCPPMiddleware => No authentication for chargepoint '{0}' configured", chargePoint.ChargePointId);
                        }

                        // Store chargepoint data
                        chargePointStatus = new ChargePointStatus(chargePoint);
                    }
                    else
                    {
                        _logger.LogWarning("OCPPMiddleware => FAILURE: Found no chargepoint with identifier={0}", chargepointIdentifier);
                    }
                }

                if (chargePointStatus != null)
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        // Match supported sub protocols
                        string subProtocol = null;
                        foreach (string supportedProtocol in SupportedProtocols)
                        {
                            if (context.WebSockets.WebSocketRequestedProtocols.Contains(supportedProtocol))
                            {
                                subProtocol = supportedProtocol;
                                break;
                            }
                        }
                        if (string.IsNullOrEmpty(subProtocol))
                        {
                            // Not matching protocol! => failure
                            string protocols = string.Empty;
                            foreach (string p in context.WebSockets.WebSocketRequestedProtocols)
                            {
                                if (string.IsNullOrEmpty(protocols)) protocols += ",";
                                protocols += p;
                            }
                            _logger.LogWarning("OCPPMiddleware => No supported sub-protocol in '{0}' from charge station '{1}'", protocols, chargepointIdentifier);
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                        else
                        {
                            chargePointStatus.Protocol = subProtocol;

                            bool statusSuccess = false;
                            try
                            {
                                _logger.LogTrace("OCPPMiddleware => Store/Update status object");

                                lock (_chargePointStatusDict)
                                {
                                    // Check if this chargepoint already/still hat a status object
                                    if (_chargePointStatusDict.ContainsKey(chargepointIdentifier))
                                    {
                                        // exists => check status
                                        if (_chargePointStatusDict[chargepointIdentifier].WebSocket.State != WebSocketState.Open)
                                        {
                                            // Closed or aborted => remove
                                            _chargePointStatusDict.Remove(chargepointIdentifier);
                                        }
                                    }

                                    _chargePointStatusDict.Add(chargepointIdentifier, chargePointStatus);
                                    statusSuccess = true;
                                }
                            }
                            catch (Exception exp)
                            {
                                _logger.LogError(exp, "OCPPMiddleware => Error storing status object in dictionary => refuse connection");
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            }

                            if (statusSuccess)
                            {
                                // Handle socket communication
                                _logger.LogTrace("OCPPMiddleware => Waiting for message...");

                                try
                                {
                                    using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync(subProtocol))
                                    {
                                        _logger.LogTrace("OCPPMiddleware => WebSocket connection with charge point '{0}'", chargepointIdentifier);
                                        chargePointStatus.WebSocket = webSocket;

                                        if (subProtocol == Protocol_OCPP21)
                                        {
                                            // OCPP V2.1
                                            await Receive21(chargePointStatus, context, dbContext);
                                        }
                                        else if (subProtocol == Protocol_OCPP201)
                                        {
                                            // OCPP V2.0
                                            await Receive20(chargePointStatus, context, dbContext);
                                        }
                                        else
                                        {
                                            // OCPP V1.6
                                            await Receive16(chargePointStatus, context, dbContext);
                                        }
                                    }
                                }
                                catch (Exception exp)
                                {
                                    if ((exp is WebSocketException || exp is TaskCanceledException) &&
                                        chargePointStatus?.WebSocket?.State != WebSocketState.Open)
                                    {
                                        _logger.LogInformation("OCPPMiddleware => WebSocket connection lost on charge point '{0}' with state '{1}' / close-status '{2}'", chargePointStatus.Id, chargePointStatus.WebSocket.State, chargePointStatus.WebSocket.CloseStatus);
                                    }
                                    else
                                    {
                                        _logger.LogTrace("OCPPMiddleware => Receive() unhandled exception '{0}'", exp.Message);
                                    }

                                    // Receive loop has ended anyway
                                    // => Close connection
                                    if (chargePointStatus?.WebSocket?.State == WebSocketState.Open)
                                    {
                                        try
                                        {
                                            await chargePointStatus?.WebSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, null, CancellationToken.None);
                                        }
                                        catch { }
                                    }
                                    // Remove chargepoint status
                                    _chargePointStatusDict.Remove(chargePointStatus.Id);
                                }
                            }
                        }
                    }
                    else
                    {
                        // no websocket request => failure
                        _logger.LogWarning("OCPPMiddleware => Non-Websocket request");
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    }
                }
                else
                {
                    // unknown chargepoint
                    _logger.LogTrace("OCPPMiddleware => no chargepoint: http 412");
                    context.Response.StatusCode = (int)HttpStatusCode.PreconditionFailed;
                }
            }
            else if (context.Request.Path.StartsWithSegments("/API"))
            {
                // Check authentication (X-API-Key)
                string apiKeyConfig = _configuration.GetValue<string>("ApiKey");
                if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                {
                    // ApiKey specified => check request
                    string apiKeyCaller = context.Request.Headers["X-API-Key"].FirstOrDefault();
                    if (apiKeyConfig == apiKeyCaller)
                    {
                        // API-Key matches
                        _logger.LogInformation("OCPPMiddleware => Success: X-API-Key matches");
                    }
                    else
                    {
                        // API-Key does NOT matches => authentication failure!!!
                        _logger.LogWarning("OCPPMiddleware => Failure: Wrong X-API-Key! Caller='{0}'", apiKeyCaller);
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                        return;
                    }
                }
                else
                {
                    // No API-Key configured => no authenticatiuon
                    _logger.LogWarning("OCPPMiddleware => No X-API-Key configured!");
                }

                // format: /API/<command>[/chargepointId[/connectorId[/parameter]]]
                string[] urlParts = context.Request.Path.Value.Split('/');

                if (urlParts.Length >= 3)
                {
                    string cmd = urlParts[2];
                    string urlChargePointId = (urlParts.Length >= 4) ? urlParts[3] : null;
                    string urlConnectorId = (urlParts.Length >= 5) ? urlParts[4] : null;
                    string urlParam = (urlParts.Length >= 6) ? urlParts[5] : null;
                    _logger.LogTrace("OCPPMiddleware => cmd='{0}' / cpId='{1}' / conId='{2}' / param='{3}' / FullPath='{4}')", cmd, urlChargePointId, urlConnectorId, urlParam, context.Request.Path.Value);

                    if (cmd == "Status")
                    {
                        try
                        {
                            List<ChargePointStatus> statusList = new List<ChargePointStatus>();
                            foreach (ChargePointStatus status in _chargePointStatusDict.Values)
                            {
                                if (status.WebSocket != null && status.WebSocket.State == WebSocketState.Open)
                                {
                                    statusList.Add(status);
                                }
                            }
                            string jsonStatus = JsonConvert.SerializeObject(statusList);
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(jsonStatus);
                        }
                        catch (Exception exp)
                        {
                            _logger.LogError(exp, "OCPPMiddleware => Error: {0}", exp.Message);
                            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        }
                    }
                    else if (cmd == "Reset")
                    {
                        if (!string.IsNullOrEmpty(urlChargePointId))
                        {
                            try
                            {
                                ChargePointStatus status = null;
                                if (_chargePointStatusDict.TryGetValue(urlChargePointId, out status))
                                {
                                    // Send message to chargepoint
                                    if (status.Protocol == Protocol_OCPP21)
                                    {
                                        // OCPP V2.1
                                        await Reset21(status, context, dbContext);
                                    }
                                    else if (status.Protocol == Protocol_OCPP201)
                                    {
                                        // OCPP V2.0
                                        await Reset20(status, context, dbContext);
                                    }
                                    else
                                    {
                                        // OCPP V1.6
                                        await Reset16(status, context, dbContext);
                                    }
                                }
                                else
                                {
                                    // Chargepoint offline
                                    _logger.LogError("OCPPMiddleware SoftReset => Chargepoint offline: {0}", urlChargePointId);
                                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                }
                            }
                            catch (Exception exp)
                            {
                                _logger.LogError(exp, "OCPPMiddleware SoftReset => Error: {0}", exp.Message);
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            }
                        }
                        else
                        {
                            _logger.LogError("OCPPMiddleware SoftReset => Missing chargepoint ID");
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    else if (cmd == "UnlockConnector")
                    {
                        if (!string.IsNullOrEmpty(urlChargePointId))
                        {
                            try
                            {
                                ChargePointStatus status = null;
                                if (_chargePointStatusDict.TryGetValue(urlChargePointId, out status))
                                {
                                    // Send message to chargepoint
                                    if (status.Protocol == Protocol_OCPP21)
                                    {
                                        // OCPP V2.1
                                        await UnlockConnector21(status, context, dbContext, urlConnectorId);
                                    }
                                    else if (status.Protocol == Protocol_OCPP201)
                                    {
                                        // OCPP V2.0
                                        await UnlockConnector20(status, context, dbContext, urlConnectorId);
                                    }
                                    else
                                    {
                                        // OCPP V1.6
                                        await UnlockConnector16(status, context, dbContext, urlConnectorId);
                                    }
                                }
                                else
                                {
                                    // Chargepoint offline
                                    _logger.LogError("OCPPMiddleware UnlockConnector => Chargepoint offline: {0}", urlChargePointId);
                                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                }
                            }
                            catch (Exception exp)
                            {
                                _logger.LogError(exp, "OCPPMiddleware UnlockConnector => Error: {0}", exp.Message);
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            }
                        }
                        else
                        {
                            _logger.LogError("OCPPMiddleware UnlockConnector => Missing chargepoint ID");
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    else if (cmd == "SetChargingLimit")
                    {
                        if (!string.IsNullOrEmpty(urlChargePointId))
                        {
                            if (!string.IsNullOrEmpty(urlParam))
                            {
                                string pattern = @"^([0-9]+)([AWaw])$";
                                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
                                Match match = regex.Match(urlParam);
                                if (match.Success && match.Groups.Count == 3)
                                {
                                    int power = int.Parse(match.Groups[1].Value);
                                    string unit = match.Groups[2].Value;

                                    try
                                    {
                                        ChargePointStatus status = null;
                                        if (_chargePointStatusDict.TryGetValue(urlChargePointId, out status))
                                        {
                                            // Send message to chargepoint
                                            if (status.Protocol == Protocol_OCPP21)
                                            {
                                                // OCPP V2.1
                                                await SetChargingProfile21(status, context, dbContext, urlConnectorId, power, unit);
                                            }
                                            else if (status.Protocol == Protocol_OCPP201)
                                            {
                                                // OCPP V2.0
                                                await SetChargingProfile20(status, context, dbContext, urlConnectorId, power, unit);
                                            }
                                            else
                                            {
                                                // OCPP V1.6
                                                await SetChargingProfile16(status, context, dbContext, urlConnectorId, power, unit);
                                            }
                                        }
                                        else
                                        {
                                            // Chargepoint offline
                                            _logger.LogError("OCPPMiddleware SetChargingProfile => Chargepoint offline: {0}", urlChargePointId);
                                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                        }
                                    }
                                    catch (Exception exp)
                                    {
                                        _logger.LogError(exp, "OCPPMiddleware SetChargingProfile => Error: {0}", exp.Message);
                                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    }
                                }
                                else
                                {
                                    _logger.LogError("OCPPMiddleware SetChargingProfile => Bad parameter (power)");
                                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                                }
                            }
                            else
                            {
                                _logger.LogError("OCPPMiddleware SetChargingProfile => Missing parameter (power)");
                                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            }
                        }
                        else
                        {
                            _logger.LogError("OCPPMiddleware SetChargingProfile => Missing chargepoint ID");
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    else if (cmd == "ClearChargingLimit")
                    {
                        if (!string.IsNullOrEmpty(urlChargePointId))
                        {
                            try
                            {
                                ChargePointStatus status = null;
                                if (_chargePointStatusDict.TryGetValue(urlChargePointId, out status))
                                {
                                    // Send message to chargepoint
                                    if (status.Protocol == Protocol_OCPP21)
                                    {
                                        // OCPP V2.1
                                        await ClearChargingProfile21(status, context, dbContext, urlConnectorId);
                                    }
                                    else if (status.Protocol == Protocol_OCPP201)
                                    {
                                        // OCPP V2.0
                                        await ClearChargingProfile20(status, context, dbContext, urlConnectorId);
                                    }
                                    else
                                    {
                                        // OCPP V1.6
                                        await ClearChargingProfile16(status, context, dbContext, urlConnectorId);
                                    }
                                }
                                else
                                {
                                    // Chargepoint offline
                                    _logger.LogError("OCPPMiddleware ClearChargingProfile => Chargepoint offline: {0}", urlChargePointId);
                                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                }
                            }
                            catch (Exception exp)
                            {
                                _logger.LogError(exp, "OCPPMiddleware ClearChargingProfile => Error: {0}", exp.Message);
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            }
                        }
                        else
                        {
                            _logger.LogError("OCPPMiddleware ClearChargingProfile => Missing chargepoint ID");
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    else if (cmd == "RemoteStartTransaction")
                    {
                        if (!string.IsNullOrEmpty(urlChargePointId))
                        {
                            // Parse connector id (int value)
                            if (!string.IsNullOrEmpty(urlConnectorId) && int.TryParse(urlConnectorId, out int connectorId))
                            {
                                if (!string.IsNullOrEmpty(urlParam))
                                {
                                    try
                                    {
                                        ChargePointStatus status = null;
                                        if (_chargePointStatusDict.TryGetValue(urlChargePointId, out status))
                                        {
                                            // Send message to chargepoint
                                            if (status.Protocol == Protocol_OCPP21)
                                            {
                                                // OCPP V2.1
                                                await RequestStartTransaction21(status, context, dbContext, urlConnectorId, urlParam);
                                            }
                                            else if (status.Protocol == Protocol_OCPP201)
                                            {
                                                // OCPP V2.0
                                                await RequestStartTransaction20(status, context, dbContext, urlConnectorId, urlParam);
                                            }
                                            else
                                            {
                                                // OCPP V1.6
                                                await RemoteStartTransaction16(status, context, dbContext, urlConnectorId, urlParam);
                                            }
                                        }
                                        else
                                        {
                                            // Chargepoint offline
                                            _logger.LogError("OCPPMiddleware RemoteStartTransaction => Chargepoint offline: {0}", urlChargePointId);
                                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                        }
                                    }
                                    catch (Exception exp)
                                    {
                                        _logger.LogError(exp, "OCPPMiddleware RemoteStartTransaction => Error: {0}", exp.Message);
                                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    }
                                }
                                else
                                {
                                    _logger.LogError("OCPPMiddleware RemoteStartTransaction => Missing tokenId");
                                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                                }
                            }
                            else
                            {
                                _logger.LogError($"OCPPMiddleware RemoteStartTransaction => Bad connector ID: '{0}'", urlConnectorId);
                                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            }
                        }
                        else
                        {
                            _logger.LogError("OCPPMiddleware RemoteStartTransaction => Missing chargepoint ID");
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    else if (cmd == "RemoteStopTransaction")
                    {
                        if (!string.IsNullOrEmpty(urlChargePointId))
                        {
                            try
                            {
                                ChargePointStatus status = null;
                                if (_chargePointStatusDict.TryGetValue(urlChargePointId, out status))
                                {
                                    if (int.TryParse(urlConnectorId, out int connectorId))
                                    {
                                        // Check last (open) transaction
                                        Transaction transaction = dbContext.Transactions
                                            .Where(t => t.ChargePointId == urlChargePointId && t.ConnectorId == connectorId)
                                            .OrderByDescending(t => t.TransactionId)
                                            .FirstOrDefault();

                                        if (transaction != null && !transaction.StopTime.HasValue)
                                        {
                                            // Send message to chargepoint
                                            if (status.Protocol == Protocol_OCPP21)
                                            {
                                                // OCPP V2.1
                                                await RequestStopTransaction21(status, context, dbContext, urlConnectorId, transaction.Uid);
                                            }
                                            else if (status.Protocol == Protocol_OCPP201)
                                            {
                                                // OCPP V2.0
                                                await RequestStopTransaction20(status, context, dbContext, urlConnectorId, transaction.Uid);
                                            }
                                            else
                                            {
                                                // OCPP V1.6
                                                await RemoteStopTransaction16(status, context, dbContext, urlConnectorId, transaction.TransactionId);
                                            }
                                        }
                                        else
                                        {
                                            _logger.LogError("OCPPMiddleware RemoteStopTransaction => connector '{0}' has no open transaction", urlConnectorId);
                                            context.Response.StatusCode = (int)HttpStatusCode.FailedDependency;
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogError("OCPPMiddleware RemoteStopTransaction => invalid connector ID: '{0}'", urlConnectorId);
                                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                                    }
                                }
                                else
                                {
                                    // Chargepoint offline
                                    _logger.LogError("OCPPMiddleware RemoteStopTransaction => Chargepoint offline: {0}", urlChargePointId);
                                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                }
                            }
                            catch (Exception exp)
                            {
                                _logger.LogError(exp, "OCPPMiddleware RemoteStopTransaction => Error: {0}", exp.Message);
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            }
                        }
                        else
                        {
                            _logger.LogError("OCPPMiddleware RemoteStopTransaction => Missing chargepoint ID");
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    else
                    {
                        // Unknown action/function
                        _logger.LogWarning("OCPPMiddleware => action/function: {0}", cmd);
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                }
            }
            else if (context.Request.Path.StartsWithSegments("/"))
            {
                try
                {
                    bool showIndexInfo = _configuration.GetValue<bool>("ShowIndexInfo");
                    if (showIndexInfo)
                    {
                        _logger.LogTrace("OCPPMiddleware => Index status page");

                        context.Response.ContentType = "text/plain";
                        await context.Response.WriteAsync(string.Format("Running...\r\n\r\n{0} chargepoints connected", _chargePointStatusDict.Values.Count));
                    }
                    else
                    {
                        _logger.LogInformation("OCPPMiddleware => Root path with deactivated index page");
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    }
                }
                catch (Exception exp)
                {
                    _logger.LogError(exp, "OCPPMiddleware => Error: {0}", exp.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                _logger.LogWarning("OCPPMiddleware => Bad path request");
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }

        /// <summary>
        /// Dumps an OCPP message in the dump dir
        /// </summary>
        private void DumpMessage(string nameSuffix, string message)
        {
            string dumpDir = _configuration.GetValue<string>("MessageDumpDir");
            if (!string.IsNullOrWhiteSpace(dumpDir))
            {
                string path = Path.Combine(dumpDir, string.Format("{0}_{1}.txt", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-ffff"), nameSuffix));
                try
                {
                    // Write incoming message into dump directory
                    File.WriteAllText(path, message);
                }
                catch (Exception exp)
                {
                    _logger.LogError(exp, "OCPPMiddleware.DumpMessage => Error dumping message '{0}' to path: '{1}'", nameSuffix, path);
                }
            }
        }
    }

    public static class OCPPMiddlewareExtensions
    {
        public static IApplicationBuilder UseOCPPMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<OCPPMiddleware>();
        }
    }
}
