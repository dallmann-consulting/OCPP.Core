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
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {
        // Supported OCPP protocols (in order)
        private static readonly string[] SupportedProtocols = { "ocpp2.0", "ocpp1.6" /*, "ocpp1.5" */};

        // RegExp for splitting ocpp message parts
        private static string MessageRegExp = "^\\[\\s*(\\d)\\s*,\\s*\"(\\w*)\"\\s*,\\s*\"(\\w*)\"\\s*,\\s*(.*)\\s*\\]$";  // ^\[\s*(\d)\s*,\s*"(\w*)"\s*,\s*"(\w*)"\s*,\s*(.*)\s*\]$

        private readonly RequestDelegate _next;
        private readonly ILoggerFactory _logFactory;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        // Dictionary with status objects for each charge point
        private static ConcurrentDictionary<string, ChargePointStatus> _chargePointStatusDict = new ConcurrentDictionary<string, ChargePointStatus>();


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
                string chargepointIdentifier = string.Empty;
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
                            context.Response.StatusCode = 400;  // Bad Request
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

                                    if (subProtocol == "ocpp2.0")
                                    {
                                        // OCPP V2.0
                                        await Receive20(chargePointStatus, context, webSocket);
                                    }
                                    else
                                    {
                                        // OCPP V1.6
                                        await Receive16(chargePointStatus, context, webSocket);
                                    }
                                }
                            }
                            else
                            {
                                _logger.LogError("OCPPMiddleware => Error storing status object in dictionary => refuse connecction");
                                context.Response.StatusCode = 500;
                            }
                        }
                    }
                    else
                    {
                        // no websocket request => failure
                        _logger.LogWarning("OCPPMiddleware => Non-Websocket request");
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    // unknown chargepoint
                    _logger.LogTrace("OCPPMiddleware => no chargepoint: http 412");
                    context.Response.StatusCode = 412;
                }
            }
            else if (context.Request.Path.StartsWithSegments("/API"))
            {
                // format: /API/<command>[/chargepointId]
                string[] urlParts = context.Request.Path.Value.Split('/');

                if (urlParts.Length >= 3)
                {
                    if (urlParts[2] == "Status")
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
                            _logger.LogError(exp, "OCPPMiddleware API => Error: {0}", exp.Message);
                            context.Response.StatusCode = 500;
                        }
                    }
                    /*
                    else if (urlParts[2] == "SoftReset")
                    {
                        try
                        {
                            

                            ChargePointStatus status = null;
                            if (_chargePointStatusDict.TryGetValue("", out status))
                            {
                                // Send message to charge point
                            }


                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync("Test");
                        }
                        catch (Exception exp)
                        {
                            _logger.LogError(exp, "Startup SoftReset => Error: {0}", exp.Message);
                            context.Response.StatusCode = 500;
                        }
                    }
                    */
                }
            }
            else
            {
                _logger.LogWarning("OCPPMiddleware => Bad path request");
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
