using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {
        // Supported OCPP protocols (in order)
        private const string Protocol_OCPP16 = "ocpp1.6";
        private const string Protocol_OCPP20 = "ocpp2.0";
        private static readonly string[] SupportedProtocols = { Protocol_OCPP20, Protocol_OCPP16 /*, "ocpp1.5" */};

        // RegExp for splitting ocpp message parts
        // ^\[\s*(\d)\s*,\s*\"(\w*)\"\s*,(?:\s*\"(\w*)\"\s*,)?\s*(.*)\s*\]$
        // Third block is optional, because responses don't have an action
        private static string MessageRegExp = "^\\[\\s*(\\d)\\s*,\\s*\"(\\w*)\"\\s*,(?:\\s*\"(\\w*)\"\\s*,)?\\s*(.*)\\s*\\]$";

        private readonly RequestDelegate _next;
        private readonly ILoggerFactory _logFactory;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        // Dictionary with status objects for each charge point
        private static ConcurrentDictionary<string, ChargePointStatus> _chargePointStatusDict = new ConcurrentDictionary<string, ChargePointStatus>();

        // Dictionary for processing asynchronous API calls
        private Dictionary<string, OCPPMessage> _requestQueue = new Dictionary<string, OCPPMessage>();

        public OCPPMiddleware(RequestDelegate next, ILoggerFactory logFactory, IConfiguration configuration)
        {
            _next = next;
            _logFactory = logFactory;
            _configuration = configuration;

            _logger = logFactory.CreateLogger("OCPPMiddleware");
        }

        public async Task Invoke(HttpContext context)
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
                    using (OCPPCoreContext dbContext = new OCPPCoreContext(_configuration))
                    {
                        ChargePoint chargePoint = dbContext.Find<ChargePoint>(chargepointIdentifier);
                        if (chargePoint != null)
                        {
                            _logger.LogInformation("OCPPMiddleware => SUCCESS: Found chargepoint with identifier={0}", chargePoint.ChargePointId);
                            chargePointStatus = new ChargePointStatus(chargePoint);
                        }
                        else
                        {
                            _logger.LogWarning("OCPPMiddleware => FAILURE: Found no chargepoint with identifier={0}", chargepointIdentifier);
                        }
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
                            // Handle socket communication
                            _logger.LogTrace("OCPPMiddleware => Waiting for message...");

                            chargePointStatus.Protocol = subProtocol;
                            if (_chargePointStatusDict.TryAdd(chargepointIdentifier, chargePointStatus))
                            {
                                using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync(subProtocol))
                                {
                                    _logger.LogTrace("OCPPMiddleware => WebSocket connection with charge point '{0}'", chargepointIdentifier);
                                    chargePointStatus.WebSocket = webSocket;

                                    if (subProtocol == Protocol_OCPP20)
                                    {
                                        // OCPP V2.0
                                        await Receive20(chargePointStatus, context);
                                    }
                                    else
                                    {
                                        // OCPP V1.6
                                        await Receive16(chargePointStatus, context);
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogError("OCPPMiddleware => Error storing status object in dictionary => refuse connecction");
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
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

                // format: /API/<command>[/chargepointId]
                string[] urlParts = context.Request.Path.Value.Split('/');

                if (urlParts.Length >= 3)
                {
                    string cmd = urlParts[2];
                    string urlChargePointId = (urlParts.Length >= 4) ? urlParts[3] : null;
                    _logger.LogTrace("OCPPMiddleware => cmd='{0}' / id='{1}' / FullPath='{2}')", cmd, urlChargePointId, context.Request.Path.Value);

                    if (cmd == "Status")
                    {
                        try
                        {
                            List<ChargePointStatus> statusList = new List<ChargePointStatus>();
                            foreach (ChargePointStatus status in _chargePointStatusDict.Values)
                            {
                                statusList.Add(status);
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
                                    if (status.Protocol == Protocol_OCPP20)
                                    {
                                        // OCPP V2.0
                                        await Reset20(status, context);
                                    }
                                    else
                                    {
                                        // OCPP V1.6
                                        await Reset16(status, context);
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
                    else
                    {
                        // Unknown action/function
                        _logger.LogWarning("OCPPMiddleware => action/function: {0}", cmd);
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    }
                }
            }
            else
            {
                _logger.LogWarning("OCPPMiddleware => Bad path request");
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
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
