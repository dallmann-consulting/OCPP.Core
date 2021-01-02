/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OCPP.Core.Server
{
    public class Startup
    {
        // Supported OCPP protocols (in order)
        private static readonly string[] SupportedProtocols = { "ocpp2.0", "ocpp1.6" /*, "ocpp1.5" */};

        // RegExp for splitting ocpp message parts
        private static string MessageRegExp = "^\\[\\s*(\\d)\\s*,\\s*\"(\\w*)\"\\s*,\\s*\"(\\w*)\"\\s*,\\s*(.*)\\s*\\]$";  // ^\[\s*(\d)\s*,\s*"(\w*)"\s*,\s*"(\w*)"\s*,\s*(.*)\s*\]$

        /// <summary>
        /// ILogger object
        /// </summary>
        private ILoggerFactory LoggerFactory { get; set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        //public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        public void Configure(IApplicationBuilder app,
                            IWebHostEnvironment env,
                            ILoggerFactory loggerFactory,
                            IConfiguration configuration)
        {
            LoggerFactory = loggerFactory;
            ILogger logger = loggerFactory.CreateLogger(typeof(Startup));
            logger.LogTrace("Configure(...)");

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Set WebSocketsOptions
            var webSocketOptions = new WebSocketOptions() 
            {
                ReceiveBufferSize = 8 * 1024
            };

            app.UseWebSockets(webSocketOptions);


            #region AcceptWebSocket
            // Handles a new connection request
            app.Use(async (context, next) =>
            {
                logger.LogInformation("Websocket request: Path='{0}'", context.Request.Path);

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
                    logger.LogInformation("Chargepoint identifier = '{0}'", chargepointIdentifier);


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
                            logger.LogWarning("No supported sub-protocol in '{0}' from charge station '{1}'", protocols, chargepointIdentifier);
                            context.Response.StatusCode = 400;  // Bad Request
                        }
                        else
                        {
                            // Handle socket communication
                            logger.LogTrace("Waiting for message...");
                            using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync(subProtocol))
                            {
                                logger.LogTrace("Receiving new message from charge point '{0}'", chargepointIdentifier);
                                if (subProtocol == "ocpp2.0")
                                {
                                    // OCPP V2.0
                                    await Receive20(chargepointIdentifier, context, webSocket);
                                }
                                else
                                {
                                    // OCPP V1.6
                                    await Receive16(chargepointIdentifier, context, webSocket);
                                }
                            }
                        }
                    }
                    else
                    {
                        // no websocket request => failure
                        logger.LogWarning("Non-Websocket request");
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    logger.LogWarning("Bad path request");
                    await next();
                }

            });
            #endregion
        }

        /// <summary>
        /// Waits for new OCPP V1.6 messages on the open websocket connection and delegates processing to a controller
        /// </summary>
        private async Task Receive16(string chargePointIdentifier, HttpContext context, WebSocket socket)
        {
            ILogger logger = LoggerFactory.CreateLogger(typeof(Startup));
            ControllerOCPP16 controller16 = new ControllerOCPP16(Configuration, LoggerFactory, chargePointIdentifier);

            byte[] buffer = new byte[1024 * 4];
            MemoryStream memStream = new MemoryStream(buffer.Length);

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result != null && result.MessageType != WebSocketMessageType.Close)
                {
                    logger.LogTrace("Receiving segment: {0} bytes (EndOfMessage={1} / MsgType={2})", result.Count, result.EndOfMessage, result.MessageType);
                    memStream.Write(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        // read complete message into byte array
                        byte[] bMessage = memStream.ToArray();
                        // reset memory stream für next message
                        memStream = new MemoryStream(buffer.Length);

                        string dumpDir = Configuration.GetValue<string>("MessageDumpDir");
                        if (!string.IsNullOrWhiteSpace(dumpDir))
                        {
                            // Write incoming message into dump directory
                            string path = Path.Combine(dumpDir, string.Format("{0}_ocpp16-in.txt", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-ffff")));
                            File.WriteAllBytes(path, bMessage);
                        }

                        string ocppMessage = UTF8Encoding.UTF8.GetString(bMessage);
                        string ocppAnswer = null;

                        Match match = Regex.Match(ocppMessage, MessageRegExp);
                        if (match != null && match.Groups != null && match.Groups.Count >= 4)
                        {
                            string messageTypeId = match.Groups[1].Value;
                            string uniqueId = match.Groups[2].Value;
                            string action = match.Groups[3].Value;
                            string jsonPaylod = match.Groups[4].Value;
                            logger.LogInformation("OCPP-Message: Type={0} / ID={1} / Action={2})", messageTypeId, uniqueId, action);

                            Messages_OCPP16.Message msgIn = new Messages_OCPP16.Message(messageTypeId, uniqueId, action, jsonPaylod);
                            Messages_OCPP16.Message msgOut = controller16.ProcessMessage(msgIn);

                            if (string.IsNullOrEmpty(msgOut.ErrorCode))
                            {
                                ocppAnswer = string.Format("[{0},\"{1}\",{2}]", msgOut.MessageType, msgOut.UniqueId, msgOut.JsonPayload);
                            }
                            else
                            {
                                ocppAnswer = string.Format("[{0},\"{1}\",\"{2}\",\"{3}\",{4}]", msgOut.MessageType, msgOut.UniqueId, msgOut.ErrorCode, msgOut.ErrorDescription, "{}");
                            }
                            logger.LogInformation("OCPP-Response: {0}", ocppAnswer);
                        }
                        else
                        {
                            logger.LogWarning("Error in RegEx-Matching: Msg={0})", ocppMessage);
                        }

                        if (string.IsNullOrEmpty(ocppAnswer))
                        {
                            // invalid message
                            ocppAnswer = string.Format("[{0},\"{1}\",\"{2}\",\"{3}\",{4}]", "4", string.Empty, Messages_OCPP16.ErrorCodes.ProtocolError, string.Empty, "{}");
                        }

                        if (!string.IsNullOrWhiteSpace(dumpDir))
                        {
                            // Write outgoing message into dump directory
                            string path = Path.Combine(dumpDir, string.Format("{0}_ocpp16-out.txt", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-ffff")));
                            File.WriteAllText(path, ocppAnswer);
                        }

                        byte[] binaryAnswer = UTF8Encoding.UTF8.GetBytes(ocppAnswer);
                        await socket.SendAsync(new ArraySegment<byte>(binaryAnswer, 0, binaryAnswer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                else
                {
                    logger.LogInformation("Receive: unexpected result: CloseStatus={0} / MessageType={1}", result?.CloseStatus, result?.MessageType);
                    await socket.CloseOutputAsync((WebSocketCloseStatus)3001, string.Empty, CancellationToken.None);
                }
            }
            logger.LogInformation("Websocket closed: State={0} / CloseStatus={1}", socket.State, socket.CloseStatus);
        }

        /// <summary>
        /// Waits for new OCPP V2.0 messages on the open websocket connection and delegates processing to a controller
        /// </summary>
        private async Task Receive20(string chargePointIdentifier, HttpContext context, WebSocket socket)
        {
            ILogger logger = LoggerFactory.CreateLogger(typeof(Startup));
            ControllerOCPP20 controller20 = new ControllerOCPP20(Configuration, LoggerFactory, chargePointIdentifier);

            byte[] buffer = new byte[1024 * 4];
            MemoryStream memStream = new MemoryStream(buffer.Length);

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result != null && result.MessageType != WebSocketMessageType.Close)
                {
                    logger.LogTrace("Receiving segment: {0} bytes (EndOfMessage={1} / MsgType={2})", result.Count, result.EndOfMessage, result.MessageType);
                    memStream.Write(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        // read complete message into byte array
                        byte[] bMessage = memStream.ToArray();
                        // reset memory stream für next message
                        memStream = new MemoryStream(buffer.Length);

                        string dumpDir = Configuration.GetValue<string>("MessageDumpDir");
                        if (!string.IsNullOrWhiteSpace(dumpDir))
                        {
                            // Write incoming message into dump directory
                            string path = Path.Combine(dumpDir, string.Format("{0}_ocpp20-in.txt", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-ffff")));
                            File.WriteAllBytes(path, bMessage);
                        }

                        string ocppMessage = UTF8Encoding.UTF8.GetString(bMessage);
                        string ocppAnswer = null;

                        Match match = Regex.Match(ocppMessage, MessageRegExp);
                        if (match != null && match.Groups != null && match.Groups.Count >= 4)
                        {
                            string messageTypeId = match.Groups[1].Value;
                            string uniqueId = match.Groups[2].Value;
                            string action = match.Groups[3].Value;
                            string jsonPaylod = match.Groups[4].Value;
                            logger.LogInformation("OCPP-Message: Type={0} / ID={1} / Action={2})", messageTypeId, uniqueId, action);

                            Messages_OCPP20.Message msgIn = new Messages_OCPP20.Message(messageTypeId, uniqueId, action, jsonPaylod);
                            Messages_OCPP20.Message msgOut = controller20.ProcessMessage(msgIn);

                            if (string.IsNullOrEmpty(msgOut.ErrorCode))
                            {
                                ocppAnswer = string.Format("[{0},\"{1}\",{2}]", msgOut.MessageType, msgOut.UniqueId, msgOut.JsonPayload);
                            }
                            else
                            {
                                ocppAnswer = string.Format("[{0},\"{1}\",\"{2}\",\"{3}\",{4}]", msgOut.MessageType, msgOut.UniqueId, msgOut.ErrorCode, msgOut.ErrorDescription, "{}");
                            }
                            logger.LogInformation("OCPP-Response: {0}", ocppAnswer);
                        }
                        else
                        {
                            logger.LogWarning("Error in RegEx-Matching: Msg={0})", ocppMessage);
                        }

                        if (string.IsNullOrEmpty(ocppAnswer))
                        {
                            // invalid message
                            ocppAnswer = string.Format("[{0},\"{1}\",\"{2}\",\"{3}\",{4}]", "4", string.Empty, Messages_OCPP20.ErrorCodes.ProtocolError, string.Empty, "{}");
                        }

                        if (!string.IsNullOrWhiteSpace(dumpDir))
                        {
                            // Write outgoing message into dump directory
                            string path = Path.Combine(dumpDir, string.Format("{0}_ocpp20-out.txt", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-ffff")));
                            File.WriteAllText(path, ocppAnswer);
                        }

                        byte[] binaryAnswer = UTF8Encoding.UTF8.GetBytes(ocppAnswer);
                        await socket.SendAsync(new ArraySegment<byte>(binaryAnswer, 0, binaryAnswer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                }
                else
                {
                    logger.LogInformation("Receive: unexpected result: CloseStatus={0} / MessageType={1}", result?.CloseStatus, result?.MessageType);
                    await socket.CloseOutputAsync((WebSocketCloseStatus)3001, string.Empty, CancellationToken.None);
                }
            }
            logger.LogInformation("Websocket closed: State={0} / CloseStatus={1}", socket.State, socket.CloseStatus);
        }
    }
}
