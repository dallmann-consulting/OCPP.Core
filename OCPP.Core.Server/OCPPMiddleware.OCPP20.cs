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

namespace OCPP.Core.Server
{
    public partial class OCPPMiddleware
    {
        /// <summary>
        /// Waits for new OCPP V2.0 messages on the open websocket connection and delegates processing to a controller
        /// </summary>
        private async Task Receive20(ChargePointStatus chargePointStatus, HttpContext context, WebSocket socket)
        {
            ILogger logger = _logFactory.CreateLogger("OCPPMiddleware.OCPP20");
            ControllerOCPP20 controller20 = new ControllerOCPP20(_configuration, _logFactory, chargePointStatus);

            byte[] buffer = new byte[1024 * 4];
            MemoryStream memStream = new MemoryStream(buffer.Length);

            while (socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result != null && result.MessageType != WebSocketMessageType.Close)
                {
                    logger.LogTrace("Startup.Receive20 => Receiving segment: {0} bytes (EndOfMessage={1} / MsgType={2})", result.Count, result.EndOfMessage, result.MessageType);
                    memStream.Write(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        // read complete message into byte array
                        byte[] bMessage = memStream.ToArray();
                        // reset memory stream für next message
                        memStream = new MemoryStream(buffer.Length);

                        string dumpDir = _configuration.GetValue<string>("MessageDumpDir");
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
                            logger.LogInformation("Startup.Receive20 => OCPP-Message: Type={0} / ID={1} / Action={2})", messageTypeId, uniqueId, action);

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
                            logger.LogInformation("Startup.Receive20 => OCPP-Response: {0}", ocppAnswer);
                        }
                        else
                        {
                            logger.LogWarning("Startup.Receive20 => Error in RegEx-Matching: Msg={0})", ocppMessage);
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
                    logger.LogInformation("Startup.Receive20 => Receive: unexpected result: CloseStatus={0} / MessageType={1}", result?.CloseStatus, result?.MessageType);
                    await socket.CloseOutputAsync((WebSocketCloseStatus)3001, string.Empty, CancellationToken.None);
                }
            }
            logger.LogInformation("Startup.Receive20 => Websocket closed: State={0} / CloseStatus={1}", socket.State, socket.CloseStatus);
            ChargePointStatus dummy;
            _chargePointStatusDict.Remove(chargePointStatus.Id, out dummy);
        }
    }
}
