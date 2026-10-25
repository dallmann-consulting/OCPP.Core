/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2026 dallmann consulting GmbH.
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
using OCPP.Core.Server.Messages_Api;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Context;

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {
        // Supported OCPP protocols (in order)
        private const string Protocol_OCPP16 = "ocpp1.6";
        private const string Protocol_OCPP201 = "ocpp2.0.1";
        private const string Protocol_OCPP21 = "ocpp2.1";
        private static readonly string[] SupportedProtocols = { Protocol_OCPP16, Protocol_OCPP201, Protocol_OCPP21 };

        // Timeout for waiting for responses from charger (for communication scheme server => charger), "RequestTimeout" in seconds
        private readonly TimeSpan _requestTimeout;


        private readonly RequestDelegate _next;
        private readonly ILoggerFactory _logFactory;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        // Dictionary with status objects for each charge point
        private static readonly ConcurrentDictionary<string, ChargePointStatus> _chargePointStatusDict = new ConcurrentDictionary<string, ChargePointStatus>();

        public OCPPMiddleware(RequestDelegate next, ILoggerFactory logFactory, IConfiguration configuration)
        {
            _next = next;
            _logFactory = logFactory;
            _configuration = configuration;
            _requestTimeout = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue<int>("RequestTimeout", 60)));

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
                string correlationId = Guid.NewGuid().ToString("N")[..8];
                using var _cidProp  = LogContext.PushProperty("CorrelationId", correlationId);
                using var _cpIdProp = LogContext.PushProperty("ChargePointId", chargepointIdentifier);

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
                            if (TryReadBasicAuthentication(context.Request.Headers["Authorization"], out string username, out string password))
                            {
                                // password is stored as hash (or as plaintext before the migration / the first login)
                                // '&' (not '&&'): the password is always checked => no timing difference for a wrong user name
                                if (ChargePointPassword.FixedTimeEquals(chargePoint.Username, username) &
                                    ChargePointPassword.Verify(chargePoint.Password, password, out bool isPlaintext))
                                {
                                    // Authentication match => OK
                                    _logger.LogInformation("OCPPMiddleware => SUCCESS: Basic authentication for chargepoint '{0}' match", chargePoint.ChargePointId);
                                    basicAuthSuccess = true;

                                    if (isPlaintext)
                                    {
                                        // stepwise migration: replace the plaintext password by a hash
                                        UpgradeChargePointPassword(dbContext, chargePoint, password);
                                    }
                                }
                                else
                                {
                                    // Authentication does NOT match => Failure
                                    _logger.LogWarning("OCPPMiddleware => FAILURE: Basic authentication for chargepoint '{0}' does NOT match", chargePoint.ChargePointId);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("OCPPMiddleware => FAILURE: Missing or invalid basic authentication header from chargepoint '{0}'", chargePoint.ChargePointId);
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
                        else if (_configuration.GetValue<bool>("RequireChargePointAuthentication", false))
                        {
                            // Authentication required but nothing configured for this chargepoint => reject
                            _logger.LogWarning("OCPPMiddleware => FAILURE: No authentication for chargepoint '{0}' configured but 'RequireChargePointAuthentication' is active", chargePoint.ChargePointId);
                            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                            return;
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

                            bool replaceExistingConnection = _configuration.GetValue<bool>("ReplaceExistingConnection", true);
                            if (!replaceExistingConnection && IsChargePointConnected(chargepointIdentifier))
                            {
                                // existing connection must not be replaced => refuse the new one
                                _logger.LogWarning("OCPPMiddleware => Chargepoint '{0}' is already connected and 'ReplaceExistingConnection' is deactivated => refuse connection", chargepointIdentifier);
                                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                            }
                            else
                            {
                                // Handle socket communication
                                _logger.LogTrace("OCPPMiddleware => Waiting for message...");

                                try
                                {
                                    using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync(subProtocol))
                                    {
                                        _logger.LogTrace("OCPPMiddleware => WebSocket connection with charge point '{0}'", chargepointIdentifier);
                                        chargePointStatus.WebSocket = webSocket;

                                        // Register the connection only after the handshake => every registered status has a WebSocket
                                        if (!RegisterChargePointStatus(chargepointIdentifier, chargePointStatus, replaceExistingConnection))
                                        {
                                            // another connection was registered in the meantime and must not be replaced
                                            _logger.LogWarning("OCPPMiddleware => Chargepoint '{0}' is already connected and 'ReplaceExistingConnection' is deactivated => close new connection", chargepointIdentifier);
                                            await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Already connected", CancellationToken.None);
                                            return;
                                        }

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
                                    if ((exp is WebSocketException || exp is TaskCanceledException || exp is OperationCanceledException) &&
                                        chargePointStatus?.WebSocket?.State != WebSocketState.Open)
                                    {
                                        _logger.LogInformation("OCPPMiddleware => WebSocket connection lost on charge point '{0}' with state '{1}' / close-status '{2}'", chargePointStatus.Id, chargePointStatus.WebSocket?.State, chargePointStatus.WebSocket?.CloseStatus);
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
                                    RemoveChargePointStatus(chargePointStatus);
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
                if (!IsApiRequestAuthorized(context))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
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
                    else if (cmd == "GetVariables" || cmd == "SetVariables")
                    {
                        // format: GET  /API/GetVariables/<chargepointId>[/<[Component.]Variable>]
                        //         GET  /API/SetVariables/<chargepointId>/<[Component.]Variable>/<value>
                        //         POST /API/GetVariables|SetVariables/<chargepointId> with JSON body (see Server-API.md)
                        bool isSet = (cmd == "SetVariables");
                        if (!string.IsNullOrEmpty(urlChargePointId))
                        {
                            ApiVariablesRequest apiRequest = null;
                            try
                            {
                                if (HttpMethods.IsPost(context.Request.Method))
                                {
                                    using StreamReader reader = new StreamReader(context.Request.Body);
                                    string body = await reader.ReadToEndAsync();
                                    apiRequest = string.IsNullOrWhiteSpace(body) ? new ApiVariablesRequest() : JsonConvert.DeserializeObject<ApiVariablesRequest>(body);
                                    if (apiRequest != null && apiRequest.Variables == null) apiRequest.Variables = new List<ApiVariableData>();
                                }
                                else
                                {
                                    apiRequest = new ApiVariablesRequest();
                                    if (!string.IsNullOrEmpty(urlConnectorId))
                                    {
                                        apiRequest.Variables.Add(ApiVariableData.FromKey(urlConnectorId, urlParam));
                                    }
                                }
                                if (apiRequest != null && string.IsNullOrEmpty(apiRequest.ReportBase))
                                {
                                    apiRequest.ReportBase = context.Request.Query["reportBase"].FirstOrDefault();
                                }
                            }
                            catch (JsonException exp)
                            {
                                _logger.LogError("OCPPMiddleware {0} => Invalid JSON body: {1}", cmd, exp.Message);
                            }

                            bool validRequest = (apiRequest != null) &&
                                                (!isSet || apiRequest.Variables.Count > 0) &&
                                                apiRequest.Variables.All(v => v != null && !string.IsNullOrWhiteSpace(v.Variable?.Name) && (!isSet || v.Value != null));
                            if (validRequest)
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
                                            if (isSet)
                                                await SetVariables21(status, context, dbContext, apiRequest);
                                            else
                                                await GetVariables21(status, context, dbContext, apiRequest);
                                        }
                                        else if (status.Protocol == Protocol_OCPP201)
                                        {
                                            // OCPP V2.0
                                            if (isSet)
                                                await SetVariables20(status, context, dbContext, apiRequest);
                                            else
                                                await GetVariables20(status, context, dbContext, apiRequest);
                                        }
                                        else
                                        {
                                            // OCPP V1.6
                                            if (isSet)
                                                await SetVariables16(status, context, dbContext, apiRequest);
                                            else
                                                await GetVariables16(status, context, dbContext, apiRequest);
                                        }
                                    }
                                    else
                                    {
                                        // Chargepoint offline
                                        _logger.LogError("OCPPMiddleware {0} => Chargepoint offline: {1}", cmd, urlChargePointId);
                                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                    }
                                }
                                catch (Exception exp)
                                {
                                    _logger.LogError(exp, "OCPPMiddleware {0} => Error: {1}", cmd, exp.Message);
                                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                }
                            }
                            else
                            {
                                _logger.LogError("OCPPMiddleware {0} => Missing or invalid variables", cmd);
                                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            }
                        }
                        else
                        {
                            _logger.LogError("OCPPMiddleware {0} => Missing chargepoint ID", cmd);
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
                    else if (cmd == "StartTransaction")
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
                                            _logger.LogError("OCPPMiddleware StartTransaction => Chargepoint offline: {0}", urlChargePointId);
                                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                        }
                                    }
                                    catch (Exception exp)
                                    {
                                        _logger.LogError(exp, "OCPPMiddleware StartTransaction => Error: {0}", exp.Message);
                                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    }
                                }
                                else
                                {
                                    _logger.LogError("OCPPMiddleware StartTransaction => Missing tokenId");
                                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                                }
                            }
                            else
                            {
                                _logger.LogError($"OCPPMiddleware StartTransaction => Bad connector ID: '{0}'", urlConnectorId);
                                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            }
                        }
                        else
                        {
                            _logger.LogError("OCPPMiddleware StartTransaction => Missing chargepoint ID");
                            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    else if (cmd == "StopTransaction")
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
                                            _logger.LogError("OCPPMiddleware StopTransaction => connector '{0}' has no open transaction", urlConnectorId);
                                            context.Response.StatusCode = (int)HttpStatusCode.FailedDependency;
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogError("OCPPMiddleware StopTransaction => invalid connector ID: '{0}'", urlConnectorId);
                                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                                    }
                                }
                                else
                                {
                                    // Chargepoint offline
                                    _logger.LogError("OCPPMiddleware StopTransaction => Chargepoint offline: {0}", urlChargePointId);
                                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                                }
                            }
                            catch (Exception exp)
                            {
                                _logger.LogError(exp, "OCPPMiddleware StopTransaction => Error: {0}", exp.Message);
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            }
                        }
                        else
                        {
                            _logger.LogError("OCPPMiddleware StopTransaction => Missing chargepoint ID");
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
        /// Reads user name and password from a basic authentication header ("Basic base64(user:password)", RFC 7617).
        /// Returns false for a missing or invalid header (=> 401, not an exception).
        /// </summary>
        internal static bool TryReadBasicAuthentication(string authHeader, out string username, out string password)
        {
            username = null;
            password = null;

            const string prefix = "Basic ";
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            byte[] decoded = new byte[authHeader.Length];
            if (!Convert.TryFromBase64String(authHeader.Substring(prefix.Length).Trim(), decoded, out int length))
            {
                return false;
            }

            // UTF-8 (OCPP 2.0.1 A00.FR.205), separated at the FIRST ':' => the password may contain ':'
            string credentials = Encoding.UTF8.GetString(decoded, 0, length);
            int separator = credentials.IndexOf(':');
            if (separator < 0)
            {
                return false;
            }
            username = credentials.Substring(0, separator);
            password = credentials.Substring(separator + 1);
            return true;
        }

        /// <summary>
        /// Stepwise migration: replaces the plaintext password of a chargepoint by its hash after a successful login.
        /// Only after the database migration (before, the column is too short for the hash).
        /// </summary>
        private void UpgradeChargePointPassword(OCPPCoreContext dbContext, ChargePoint chargePoint, string password)
        {
            try
            {
                if (!dbContext.IsMigrationApplied(MigrationExtensions.ChargePointPasswordHashMigration))
                {
                    _logger.LogWarning("OCPPMiddleware => Password of chargepoint '{0}' remains plaintext: database migration '{1}' is pending", chargePoint.ChargePointId, MigrationExtensions.ChargePointPasswordHashMigration);
                    return;
                }

                chargePoint.Password = ChargePointPassword.Hash(password);
                dbContext.SaveChanges();
                _logger.LogInformation("OCPPMiddleware => Password of chargepoint '{0}' is stored as hash now", chargePoint.ChargePointId);
            }
            catch (Exception exp)
            {
                // login is successful anyway
                _logger.LogError(exp, "OCPPMiddleware => Error storing the password hash of chargepoint '{0}': {1}", chargePoint.ChargePointId, exp.Message);
            }
        }

        /// <summary>
        /// Checks the authorization of an API request:
        /// - ApiKey configured => the caller must send the matching "X-API-Key" header
        /// - no ApiKey configured => only local requests are allowed (unless "AllowUnauthenticatedApi" is set)
        /// </summary>
        private bool IsApiRequestAuthorized(HttpContext context)
        {
            string apiKeyConfig = _configuration.GetValue<string>("ApiKey");
            if (!string.IsNullOrWhiteSpace(apiKeyConfig))
            {
                string apiKeyCaller = context.Request.Headers["X-API-Key"].FirstOrDefault();
                if (FixedTimeEquals(apiKeyConfig, apiKeyCaller))
                {
                    _logger.LogTrace("OCPPMiddleware => Success: X-API-Key matches");
                    return true;
                }

                // the key of the caller is not logged (could be a mistyped real key)
                _logger.LogWarning("OCPPMiddleware => Failure: Missing or wrong X-API-Key from '{0}'", context.Connection.RemoteIpAddress);
                return false;
            }

            if (_configuration.GetValue<bool>("AllowUnauthenticatedApi", false))
            {
                _logger.LogTrace("OCPPMiddleware => No X-API-Key configured and 'AllowUnauthenticatedApi' is active => request allowed");
                return true;
            }

            if (IsLocalRequest(context))
            {
                _logger.LogTrace("OCPPMiddleware => No X-API-Key configured => local request allowed");
                return true;
            }

            _logger.LogWarning("OCPPMiddleware => Failure: No X-API-Key configured => API request from '{0}' rejected (only local requests are allowed)", context.Connection.RemoteIpAddress);
            return false;
        }

        /// <summary>
        /// True if the request comes from the local computer (loopback) and not via a reverse proxy
        /// </summary>
        private static bool IsLocalRequest(HttpContext context)
        {
            // A reverse proxy on the same computer connects via loopback => forwarded requests are never local
            if (context.Request.Headers.ContainsKey("X-Forwarded-For") ||
                context.Request.Headers.ContainsKey("Forwarded"))
            {
                return false;
            }

            IPAddress remoteIp = context.Connection.RemoteIpAddress;
            if (remoteIp == null) return false;
            if (remoteIp.IsIPv4MappedToIPv6) remoteIp = remoteIp.MapToIPv4();
            return IPAddress.IsLoopback(remoteIp);
        }

        /// <summary>
        /// Compares two secrets in constant time (no timing attacks on the key)
        /// </summary>
        private static bool FixedTimeEquals(string expected, string actual)
        {
            if (actual == null) return false;
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
        }

        /// <summary>
        /// Sends a request to the chargepoint and waits (asynchronously) for the answer.
        /// Returns the result of the answer processing in the controller - or the reason without answer
        /// (timeout, disconnect, API caller aborted or CALLERROR of the chargepoint).
        /// Only one request at a time per chargepoint: waits until the previous request is answered or timed out.
        /// The API caller waits at most "RequestTimeout" in total (waiting for the previous request included):
        /// max. half of it for the previous request => at least the other half remains for the answer
        /// (a request isn't sent just before the API caller gets a timeout).
        /// </summary>
        private async Task<OcppRequestResult> SendRequestAndWait(ChargePointStatus chargePointStatus, string action, object request, ILogger logger, CancellationToken cancellationToken)
        {
            if (chargePointStatus.WebSocket?.State != WebSocketState.Open)
            {
                logger.LogInformation("OCPPMiddleware => {0}: Chargepoint disconnected (ChargePoint='{1}')", action, chargePointStatus.Id);
                return OcppRequestResult.FromNoAnswer(ApiVariableStatus.Disconnected);
            }

            Stopwatch callerWait = Stopwatch.StartNew();
            if (chargePointStatus.RequestLock.CurrentCount == 0)
            {
                logger.LogInformation("OCPPMiddleware => {0}: Waiting for the answer to the previous request (ChargePoint='{1}')", action, chargePointStatus.Id);
            }
            bool locked;
            try
            {
                locked = await chargePointStatus.RequestLock.WaitAsync(_requestTimeout / 2, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("OCPPMiddleware => {0}: API caller aborted while waiting for the previous request => not sent (ChargePoint='{1}')", action, chargePointStatus.Id);
                return OcppRequestResult.FromNoAnswer(NoAnswerStatus(chargePointStatus));
            }
            if (!locked)
            {
                logger.LogInformation("OCPPMiddleware => {0}: Timeout - previous request still not answered => not sent (ChargePoint='{1}')", action, chargePointStatus.Id);
                return OcppRequestResult.FromNoAnswer(NoAnswerStatus(chargePointStatus));
            }

            // true: the lock is released when the request is answered or timed out (=> ReleaseRequestLockAsync)
            bool requestSent = false;
            try
            {
                return await SendLockedRequestAndWait(chargePointStatus, action, request, logger, callerWait, () => requestSent = true, cancellationToken);
            }
            finally
            {
                if (!requestSent)
                {
                    chargePointStatus.RequestLock.Release();
                }
            }
        }

        /// <summary>
        /// Sends the request (the caller holds the RequestLock) and waits for the answer
        /// </summary>
        private async Task<OcppRequestResult> SendLockedRequestAndWait(ChargePointStatus chargePointStatus, string action, object request, ILogger logger,
            Stopwatch callerWait, Action onSent, CancellationToken cancellationToken)
        {
            if (chargePointStatus.WebSocket?.State != WebSocketState.Open)
            {
                // disconnected while waiting for the previous request
                logger.LogInformation("OCPPMiddleware => {0}: Chargepoint disconnected (ChargePoint='{1}')", action, chargePointStatus.Id);
                return OcppRequestResult.FromNoAnswer(ApiVariableStatus.Disconnected);
            }

            OCPPMessage msgOut = new OCPPMessage();
            msgOut.MessageType = OcppMessageType.Call;
            msgOut.Action = action;
            // OCPP 1.6: short id for firmware that truncates the echoed id
            msgOut.UniqueId = (chargePointStatus.Protocol == Protocol_OCPP21 || chargePointStatus.Protocol == Protocol_OCPP201) ? Guid.NewGuid().ToString("N") : NewShortUniqueId();
            msgOut.JsonPayload = JsonConvert.SerializeObject(request);
            // Continuations must not run synchronously in the receive loop of the chargepoint (=> SetResult in the controller)
            msgOut.TaskCompletionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            // store message with MsgId for later answer processing (=> send answer to API caller)
            chargePointStatus.PendingRequests.TryAdd(msgOut.UniqueId, msgOut);
            try
            {
                // Send OCPP message with optional logging/dump
                await SendOcppMessage(msgOut, logger, chargePointStatus);
            }
            catch (Exception exp) when (exp is WebSocketException || exp is ObjectDisposedException)
            {
                // connection closed in the meantime
                chargePointStatus.PendingRequests.TryRemove(msgOut.UniqueId, out _);
                logger.LogInformation("OCPPMiddleware => {0}: Sending failed - chargepoint disconnected (ChargePoint='{1}'): {2}", action, chargePointStatus.Id, exp.Message);
                return OcppRequestResult.FromNoAnswer(ApiVariableStatus.Disconnected);
            }

            // The chargepoint processes the request even if the API caller aborts
            // => the next request is only sent when this one is answered, timed out or the connection is closed
            onSent();
            _ = ReleaseRequestLockAsync(chargePointStatus, msgOut);

            // Wait for asynchronous chargepoint response and processing (API caller: "RequestTimeout" in total)
            TimeSpan remaining = _requestTimeout - callerWait.Elapsed;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            Task completedTask = await Task.WhenAny(msgOut.TaskCompletionSource.Task, Task.Delay(remaining, cancellationToken));
            if (completedTask == msgOut.TaskCompletionSource.Task)
            {
                string result = await msgOut.TaskCompletionSource.Task;
                if (result != null)
                {
                    return OcppRequestResult.FromAnswer(result);
                }
                if (!string.IsNullOrEmpty(msgOut.ErrorCode))
                {
                    // completed by a CALLERROR of the chargepoint
                    return OcppRequestResult.FromCallError(msgOut.ErrorCode, msgOut.ErrorDescription);
                }
                if (msgOut.AnswerInvalid)
                {
                    // answer of the chargepoint couldn't be processed
                    return OcppRequestResult.FromNoAnswer(ApiVariableStatus.InvalidAnswer);
                }
                // completed by AbortPendingRequests
                logger.LogInformation("OCPPMiddleware => {0}: Chargepoint disconnected while waiting for the answer (ChargePoint='{1}')", action, chargePointStatus.Id);
                return OcppRequestResult.FromNoAnswer(ApiVariableStatus.Disconnected);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("OCPPMiddleware => {0}: API caller aborted (ChargePoint='{1}')", action, chargePointStatus.Id);
            }
            else
            {
                logger.LogInformation("OCPPMiddleware => {0}: Timeout (ChargePoint='{1}')", action, chargePointStatus.Id);
            }
            return OcppRequestResult.FromNoAnswer(NoAnswerStatus(chargePointStatus));
        }

        /// <summary>
        /// Releases the RequestLock of the chargepoint when the request is answered, timed out ("RequestTimeout" after sending)
        /// or the connection is closed (AbortPendingRequests) - independent of the API caller
        /// </summary>
        private async Task ReleaseRequestLockAsync(ChargePointStatus chargePointStatus, OCPPMessage msgOut)
        {
            try
            {
                await Task.WhenAny(msgOut.TaskCompletionSource.Task, Task.Delay(_requestTimeout));
            }
            finally
            {
                // later answers are only logged
                chargePointStatus.PendingRequests.TryRemove(msgOut.UniqueId, out _);
                chargePointStatus.RequestLock.Release();
            }
        }

        /// <summary>
        /// API result status when the chargepoint didn't answer: "Disconnected" or "Timeout"
        /// </summary>
        private static string NoAnswerStatus(ChargePointStatus chargePointStatus)
        {
            return (chargePointStatus.WebSocket?.State == WebSocketState.Open) ? ApiVariableStatus.Timeout : ApiVariableStatus.Disconnected;
        }

        /// <summary>
        /// Processes a received OCPP-J frame (same for all OCPP versions):
        /// requests are processed by the controller and answered, answers are assigned to the pending request.
        /// </summary>
        /// <param name="processRequest">Controller: processes a request and returns the answer</param>
        /// <param name="processAnswer">Controller: processes an answer (incoming message, original request)</param>
        /// <param name="invalidMessageErrorCode">Error code of the OCPP version for an invalid message</param>
        private async Task ProcessIncomingMessage(ChargePointStatus chargePointStatus, string ocppMessage, ILogger logger,
            Func<OCPPMessage, OCPPMessage> processRequest, Action<OCPPMessage, OCPPMessage> processAnswer, string invalidMessageErrorCode)
        {
            if (!OcppFrame.TryParse(ocppMessage, out OCPPMessage msgIn, out string parseError))
            {
                logger.LogWarning("OCPPMiddleware => Invalid message from chargepoint '{0}': {1} / Msg: {2}", chargePointStatus.Id, parseError, ocppMessage);

                // Answer requests (or messages with unknown type) with a CALLERROR - not answers of the chargepoint
                if (msgIn.MessageType != OcppMessageType.CallResult &&
                    msgIn.MessageType != OcppMessageType.CallError &&
                    msgIn.MessageType != OcppMessageType.CallResultError)
                {
                    OCPPMessage msgError = new OCPPMessage();
                    msgError.MessageType = OcppMessageType.CallError;
                    // message ID can't be read => "-1" (OCPP-J)
                    msgError.UniqueId = string.IsNullOrEmpty(msgIn.UniqueId) ? OcppFrame.UnknownMessageId : msgIn.UniqueId;
                    msgError.ErrorCode = invalidMessageErrorCode;
                    msgError.ErrorDescription = parseError;
                    await SendOcppMessage(msgError, logger, chargePointStatus);
                }
                return;
            }

            logger.LogInformation("OCPPMiddleware => OCPP-Message: Type={0} / ID={1} / Action={2}", msgIn.MessageType, msgIn.UniqueId, msgIn.Action);

            // Send raw incoming messages to extensions
            _ = Task.Run(() =>
            {
                ProcessRawIncomingMessageSinks(chargePointStatus.Protocol, chargePointStatus.Id, msgIn);
            });

            switch (msgIn.MessageType)
            {
                case OcppMessageType.Call:
                    // Request from chargepoint to OCPP server
                    OCPPMessage msgOut = processRequest(msgIn);
                    // Send OCPP message with optional logging/dump
                    await SendOcppMessage(msgOut, logger, chargePointStatus);
                    break;

                case OcppMessageType.CallResult:
                    // Answer from chargepoint => process in controller
                    if (chargePointStatus.PendingRequests.TryRemove(msgIn.UniqueId, out OCPPMessage msgRequest))
                    {
                        processAnswer(msgIn, msgRequest);

                        // The controller sets the API result when the answer was processed successfully.
                        // Not set (e.g. invalid content of the answer) => the API caller gets an error immediately (instead of a timeout)
                        if (msgRequest.TaskCompletionSource != null && !msgRequest.TaskCompletionSource.Task.IsCompleted)
                        {
                            logger.LogWarning("OCPPMiddleware => Answer of chargepoint '{0}' to '{1}' couldn't be processed / Msg: {2}", chargePointStatus.Id, msgRequest.Action, ocppMessage);
                            msgRequest.AnswerInvalid = true;
                            msgRequest.TaskCompletionSource.TrySetResult(null);
                        }
                    }
                    else
                    {
                        logger.LogError("OCPPMiddleware => Request not found (late answer after timeout?) / Msg: {0}", ocppMessage);
                    }
                    break;

                case OcppMessageType.CallError:
                    // Error answer from chargepoint => API caller gets the error immediately
                    if (chargePointStatus.PendingRequests.TryRemove(msgIn.UniqueId, out OCPPMessage msgFailedRequest))
                    {
                        logger.LogWarning("OCPPMiddleware => Chargepoint '{0}' answered '{1}' with CALLERROR: {2} / {3}", chargePointStatus.Id, msgFailedRequest.Action, msgIn.ErrorCode, msgIn.ErrorDescription);
                        msgFailedRequest.ErrorCode = msgIn.ErrorCode;
                        msgFailedRequest.ErrorDescription = msgIn.ErrorDescription;
                        msgFailedRequest.TaskCompletionSource?.TrySetResult(null);
                    }
                    else
                    {
                        logger.LogError("OCPPMiddleware => Request not found for CALLERROR (late answer after timeout?) / Msg: {0}", ocppMessage);
                    }
                    break;

                case OcppMessageType.CallResultError:
                    // (OCPP 2.1) the chargepoint couldn't process an answer of the server
                    logger.LogWarning("OCPPMiddleware => Chargepoint '{0}' couldn't process an answer (CALLRESULTERROR): {1} / {2} / Msg: {3}", chargePointStatus.Id, msgIn.ErrorCode, msgIn.ErrorDescription, ocppMessage);
                    break;

                case OcppMessageType.Send:
                    // (OCPP 2.1) unconfirmed message without response - not supported yet
                    logger.LogInformation("OCPPMiddleware => SEND message '{0}' from chargepoint '{1}' ignored", msgIn.Action, chargePointStatus.Id);
                    break;
            }
        }

        /// <summary>
        /// Sends an OCPP message (frame with correct JSON escaping, optional dump, extensions)
        /// </summary>
        private async Task SendOcppFrame(OCPPMessage msg, ILogger logger, ChargePointStatus chargePointStatus, string dumpName)
        {
            // Send raw outgoing messages to extensions
            _ = Task.Run(() =>
            {
                ProcessRawOutgoingMessageSinks(chargePointStatus.Protocol, chargePointStatus.Id, msg);
            });

            string ocppTextMessage = OcppFrame.Serialize(msg);
            logger.LogTrace("OCPPMiddleware => SendOcppMessage: {0}", ocppTextMessage);

            // write message (async) to dump directory
            _ = Task.Run(() =>
            {
                DumpMessage(dumpName, ocppTextMessage);
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

        /// <summary>
        /// True if the chargepoint has a registered connection with an open WebSocket
        /// </summary>
        private static bool IsChargePointConnected(string chargepointIdentifier)
        {
            return _chargePointStatusDict.TryGetValue(chargepointIdentifier, out ChargePointStatus existingStatus) &&
                   existingStatus.WebSocket?.State == WebSocketState.Open;
        }

        /// <summary>
        /// Registers the status object of a new connection.
        /// An existing open connection of the same chargepoint (e.g. a dead connection after a network failure
        /// that still seems to be open) is replaced and aborted - or the new connection is refused (returns false)
        /// if "ReplaceExistingConnection" is deactivated.
        /// </summary>
        private bool RegisterChargePointStatus(string chargepointIdentifier, ChargePointStatus newStatus, bool replaceExistingConnection)
        {
            while (true)
            {
                if (_chargePointStatusDict.TryAdd(chargepointIdentifier, newStatus))
                {
                    return true;
                }

                if (!_chargePointStatusDict.TryGetValue(chargepointIdentifier, out ChargePointStatus existingStatus))
                {
                    // removed in the meantime => try again
                    continue;
                }

                bool existingIsOpen = existingStatus.WebSocket?.State == WebSocketState.Open;
                if (existingIsOpen && !replaceExistingConnection)
                {
                    return false;
                }

                // atomic: only replaced if the existing entry is still the same object
                if (_chargePointStatusDict.TryUpdate(chargepointIdentifier, newStatus, existingStatus))
                {
                    if (existingIsOpen)
                    {
                        _logger.LogWarning("OCPPMiddleware => Connection of chargepoint '{0}' is replaced by a new connection", chargepointIdentifier);
                        try
                        {
                            // Abort (not close): a graceful close could hang on a dead TCP connection
                            existingStatus.WebSocket.Abort();
                        }
                        catch (Exception exp)
                        {
                            _logger.LogTrace("OCPPMiddleware => Error aborting replaced connection: {0}", exp.Message);
                        }
                    }
                    // pending API requests of the old connection end immediately ("Disconnected")
                    AbortPendingRequests(existingStatus);
                    return true;
                }
            }
        }

        /// <summary>
        /// Removes the status object of a connection from the dictionary - but only this object:
        /// after a reconnect the dictionary may already contain the status of the new connection.
        /// Searches by object (not by key), because the key is the identifier from the URL.
        /// </summary>
        private static void RemoveChargePointStatus(ChargePointStatus chargePointStatus)
        {
            foreach (KeyValuePair<string, ChargePointStatus> entry in _chargePointStatusDict)
            {
                if (ReferenceEquals(entry.Value, chargePointStatus))
                {
                    // atomic: only removed if the key still has this value
                    _chargePointStatusDict.TryRemove(entry);
                }
            }
        }

        /// <summary>
        /// Ends all requests and reports of a chargepoint that wait for an answer (after disconnect)
        /// </summary>
        private static void AbortPendingRequests(ChargePointStatus chargePointStatus)
        {
            foreach (string uniqueId in chargePointStatus.PendingRequests.Keys)
            {
                if (chargePointStatus.PendingRequests.TryRemove(uniqueId, out OCPPMessage msg))
                {
                    msg.TaskCompletionSource?.TrySetResult(null);
                }
            }
            AbortPendingReports(chargePointStatus, ApiReportStatus.Disconnected);
        }

        /// <summary>
        /// Closes the output of the WebSocket (synchronized with other send operations)
        /// </summary>
        private static async Task CloseOutputAsync(ChargePointStatus chargePointStatus, WebSocketCloseStatus closeStatus)
        {
            await chargePointStatus.SendLock.WaitAsync();
            try
            {
                await chargePointStatus.WebSocket.CloseOutputAsync(closeStatus, string.Empty, CancellationToken.None);
            }
            finally
            {
                chargePointStatus.SendLock.Release();
            }
        }

        /// <summary>
        /// Waits (asynchronously) until all parts of a report are received.
        /// Ends after an idle timeout (no new part) or the max. duration.
        /// Returns null (complete or timeout => see PendingReport.CreateResponse) or an abort status (ApiReportStatus)
        /// </summary>
        private async Task<string> WaitForReport(PendingReport report, ChargePointStatus chargePointStatus, ILogger logger, CancellationToken cancellationToken)
        {
            TimeSpan idleTimeout = TimeSpan.FromSeconds(_configuration.GetValue<int>("ReportIdleTimeout", 30));
            TimeSpan maxDuration = TimeSpan.FromSeconds(_configuration.GetValue<int>("ReportMaxDuration", 300));
            DateTime started = DateTime.UtcNow;

            while (true)
            {
                // the idle time starts at the earliest when waiting starts (=> after the GetBaseReport response)
                DateTime lastActivity = (report.LastActivity > started) ? report.LastActivity : started;
                DateTime now = DateTime.UtcNow;
                TimeSpan idleWait = lastActivity + idleTimeout - now;
                TimeSpan totalWait = started + maxDuration - now;
                TimeSpan wait = (idleWait < totalWait) ? idleWait : totalWait;
                if (wait <= TimeSpan.Zero)
                {
                    logger.LogInformation("OCPPMiddleware => Report {0}: {1} timeout (ChargePoint='{2}')", report.RequestId, (idleWait <= TimeSpan.Zero) ? "idle" : "max. duration", chargePointStatus.Id);
                    return null;
                }

                Task completedTask = await Task.WhenAny(report.Completed.Task, Task.Delay(wait, cancellationToken));
                if (completedTask == report.Completed.Task)
                {
                    return await report.Completed.Task;
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("OCPPMiddleware => Report {0}: API caller aborted (ChargePoint='{1}')", report.RequestId, chargePointStatus.Id);
                    return null;
                }
            }
        }

        /// <summary>
        /// Aborts all pending reports of a chargepoint (e.g. after disconnect)
        /// </summary>
        private static void AbortPendingReports(ChargePointStatus chargePointStatus, string status)
        {
            foreach (PendingReport report in chargePointStatus.PendingReports.Values)
            {
                report.Abort(status);
            }
        }

        /// <summary>
        /// Sends an OCPP message with the protocol of the chargepoint
        /// </summary>
        private Task SendOcppMessage(OCPPMessage msg, ILogger logger, ChargePointStatus chargePointStatus)
        {
            if (chargePointStatus.Protocol == Protocol_OCPP21)
            {
                return SendOcpp21Message(msg, logger, chargePointStatus);
            }
            else if (chargePointStatus.Protocol == Protocol_OCPP201)
            {
                return SendOcpp20Message(msg, logger, chargePointStatus);
            }
            return SendOcpp16Message(msg, logger, chargePointStatus);
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
