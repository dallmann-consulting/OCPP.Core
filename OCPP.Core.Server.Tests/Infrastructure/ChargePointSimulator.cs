using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.TestHost;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OCPP.Core.Server.Tests.Infrastructure
{
    /// <summary>
    /// Result of a CALL from the simulated charge point to the server
    /// </summary>
    public sealed record OcppResult(int MessageType, JObject? Payload, string? ErrorCode, string? ErrorDescription)
    {
        public bool IsError => MessageType == 4;
    }

    /// <summary>
    /// A CALL from the server to the simulated charge point
    /// </summary>
    public sealed record ServerCall(string UniqueId, string Action, JObject Payload);

    /// <summary>
    /// Simulates a charge point: sends CALLs to the server and answers CALLs from the server
    /// </summary>
    public sealed class ChargePointSimulator : IAsyncDisposable
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        private readonly WebSocket _webSocket;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<OcppResult>> _pendingCalls = new ConcurrentDictionary<string, TaskCompletionSource<OcppResult>>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task _receiveTask;

        private ChargePointSimulator(WebSocket webSocket, string chargePointId)
        {
            _webSocket = webSocket;
            ChargePointId = chargePointId;
            _receiveTask = Task.Run(ReceiveLoop);
        }

        public string ChargePointId { get; }

        public string? Protocol => _webSocket.SubProtocol;

        public WebSocketState State => _webSocket.State;

        /// <summary>
        /// All CALLs received from the server (in order of arrival)
        /// </summary>
        public ConcurrentQueue<ServerCall> ReceivedCalls { get; } = new ConcurrentQueue<ServerCall>();

        /// <summary>
        /// Return value of <see cref="ServerCallHandler"/>: don't answer the CALL (e.g. dead connection)
        /// </summary>
        public static readonly object NoAnswer = new object();

        /// <summary>
        /// Creates the answer payload for a CALL from the server.
        /// Returning null sends a CALLERROR "NotImplemented", returning <see cref="NoAnswer"/> sends nothing.
        /// </summary>
        public Func<ServerCall, object?> ServerCallHandler { get; set; } = call => new { status = "Accepted" };

        /// <summary>
        /// Opens the WebSocket connection. Throws when the server rejects the handshake
        /// (the exception message contains the HTTP status code).
        /// </summary>
        public static async Task<ChargePointSimulator> ConnectAsync(TestServer server, string chargePointId, string protocol, string? username = null, string? password = null)
        {
            WebSocketClient client = server.CreateWebSocketClient();
            if (!string.IsNullOrEmpty(protocol))
            {
                client.SubProtocols.Add(protocol);
            }
            if (username != null || password != null)
            {
                string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                client.ConfigureRequest = request => request.Headers["Authorization"] = "Basic " + credentials;
            }

            WebSocket webSocket = await client.ConnectAsync(new Uri(server.BaseAddress, "OCPP/" + Uri.EscapeDataString(chargePointId)), CancellationToken.None);
            return new ChargePointSimulator(webSocket, chargePointId);
        }

        /// <summary>
        /// Sends a CALL and waits for the CALLRESULT/CALLERROR
        /// </summary>
        public Task<OcppResult> CallAsync(string action, object payload, TimeSpan? timeout = null)
        {
            string uniqueId = Guid.NewGuid().ToString("N");
            string frame = JsonConvert.SerializeObject(new object[] { 2, uniqueId, action, payload });
            return SendFrameAndWaitAsync(uniqueId, frame, timeout);
        }

        /// <summary>
        /// Sends a raw frame (e.g. invalid messages) and waits for an answer with the given unique id
        /// </summary>
        public async Task<OcppResult> SendFrameAndWaitAsync(string uniqueId, string frame, TimeSpan? timeout = null)
        {
            TaskCompletionSource<OcppResult> tcs = new TaskCompletionSource<OcppResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingCalls[uniqueId] = tcs;
            try
            {
                await SendTextAsync(frame);
                return await tcs.Task.WaitAsync(timeout ?? DefaultTimeout);
            }
            finally
            {
                _pendingCalls.TryRemove(uniqueId, out _);
            }
        }

        /// <summary>
        /// Sends a raw frame without waiting for an answer
        /// </summary>
        public Task SendTextAsync(string frame)
        {
            return SendAsync(frame);
        }

        /// <summary>
        /// Waits until the server has sent a CALL with the given action
        /// </summary>
        public async Task<ServerCall> WaitForServerCallAsync(string action, TimeSpan? timeout = null)
        {
            DateTime end = DateTime.UtcNow + (timeout ?? DefaultTimeout);
            while (DateTime.UtcNow < end)
            {
                ServerCall? call = ReceivedCalls.FirstOrDefault(c => c.Action == action);
                if (call != null) return call;
                await Task.Delay(20);
            }
            throw new TimeoutException($"No server call '{action}' received");
        }

        private async Task SendAsync(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            await _sendLock.WaitAsync();
            try
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoop()
        {
            byte[] buffer = new byte[8 * 1024];
            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    using MemoryStream message = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            return;
                        }
                        message.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    await HandleFrameAsync(Encoding.UTF8.GetString(message.ToArray()));
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException)
            {
            }
            finally
            {
                foreach (TaskCompletionSource<OcppResult> tcs in _pendingCalls.Values)
                {
                    tcs.TrySetException(new WebSocketException("Connection closed"));
                }
            }
        }

        private async Task HandleFrameAsync(string text)
        {
            JArray frame = JArray.Parse(text);
            int messageType = frame[0].Value<int>();
            string uniqueId = frame[1].Value<string>()!;

            switch (messageType)
            {
                case 2:
                    // CALL from the server => answer
                    ServerCall call = new ServerCall(uniqueId, frame[2].Value<string>()!, (JObject)frame[3]);
                    ReceivedCalls.Enqueue(call);
                    object? answer = ServerCallHandler(call);
                    if (ReferenceEquals(answer, NoAnswer)) break;
                    string response = (answer != null)
                        ? JsonConvert.SerializeObject(new object[] { 3, uniqueId, answer })
                        : JsonConvert.SerializeObject(new object[] { 4, uniqueId, "NotImplemented", "", new { } });
                    await SendAsync(response);
                    break;

                case 3:
                    if (_pendingCalls.TryGetValue(uniqueId, out TaskCompletionSource<OcppResult>? resultTcs))
                    {
                        resultTcs.TrySetResult(new OcppResult(3, frame[2] as JObject, null, null));
                    }
                    break;

                case 4:
                    if (_pendingCalls.TryGetValue(uniqueId, out TaskCompletionSource<OcppResult>? errorTcs))
                    {
                        errorTcs.TrySetResult(new OcppResult(4, frame.Count > 4 ? frame[4] as JObject : null, frame[2].Value<string>(), frame[3].Value<string>()));
                    }
                    break;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    using CancellationTokenSource closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Test end", closeTimeout.Token);
                }
            }
            catch (Exception)
            {
                // connection already gone
            }
            _cts.Cancel();
            try
            {
                await _receiveTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
            }
            _webSocket.Dispose();
            _cts.Dispose();
        }
    }
}
