using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Server with a short "RequestTimeout" (4 s => max. 2 s waiting for the previous request)
    /// </summary>
    public class ShortRequestTimeoutServerFactory : OcppServerFactory
    {
        public const int RequestTimeoutSeconds = 4;

        protected override void ConfigureSettings(Dictionary<string, string?> settings)
        {
            settings["RequestTimeout"] = RequestTimeoutSeconds.ToString();
        }
    }

    /// <summary>
    /// Only one request (CALL) from the server at a time per charge point.
    /// OCPP-J: no new CALL until the previous one is answered or timed out.
    /// </summary>
    public class RequestSerializationTests : IClassFixture<ShortRequestTimeoutServerFactory>
    {
        private readonly ShortRequestTimeoutServerFactory _factory;
        private readonly HttpClient _client;

        public RequestSerializationTests(ShortRequestTimeoutServerFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task SecondRequest_IsSentAfterTheAnswer(string protocol)
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            cp.ServerCallHandler = _ => ChargePointSimulator.NoAnswer;

            Task<HttpResponseMessage> reset = _client.GetAsync($"/API/Reset/{cp.ChargePointId}", TestContext.Current.CancellationToken);
            ServerCall resetCall = await cp.WaitForServerCallAsync("Reset");
            Task<HttpResponseMessage> unlock = _client.GetAsync($"/API/UnlockConnector/{cp.ChargePointId}/1", TestContext.Current.CancellationToken);

            // the second request waits for the answer to the first one
            await Task.Delay(500, TestContext.Current.CancellationToken);
            Assert.Single(cp.ReceivedCalls);

            await AnswerAsync(cp, resetCall, new { status = "Accepted" });
            Assert.Equal("Accepted", await StatusAsync(reset));

            ServerCall unlockCall = await cp.WaitForServerCallAsync("UnlockConnector");
            await AnswerAsync(cp, unlockCall, new { status = "Unlocked" });
            HttpResponseMessage unlockResponse = await unlock;
            Assert.Equal(HttpStatusCode.OK, unlockResponse.StatusCode);
            Assert.Equal(2, cp.ReceivedCalls.Count);
        }

        [Fact]
        public async Task NoAnswer_SecondRequestIsNotSent()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp201);
            cp.ServerCallHandler = _ => ChargePointSimulator.NoAnswer;

            Task<HttpResponseMessage> reset = _client.GetAsync($"/API/Reset/{cp.ChargePointId}", TestContext.Current.CancellationToken);
            await cp.WaitForServerCallAsync("Reset");
            Task<HttpResponseMessage> unlock = _client.GetAsync($"/API/UnlockConnector/{cp.ChargePointId}/1", TestContext.Current.CancellationToken);

            Assert.Equal("Timeout", await StatusAsync(unlock));
            Assert.Equal("Timeout", await StatusAsync(reset));
            Assert.Single(cp.ReceivedCalls);

            // after the timeout of the first request the next one is sent
            cp.ServerCallHandler = _ => new { status = "Accepted" };
            await Task.Delay(500, TestContext.Current.CancellationToken);
            Assert.Equal("Accepted", await StatusAsync(_client.GetAsync($"/API/Reset/{cp.ChargePointId}", TestContext.Current.CancellationToken)));
            Assert.Equal(2, cp.ReceivedCalls.Count);
        }

        [Fact]
        public async Task Disconnect_EndsWaitingRequests()
        {
            string chargePointId = _factory.AddChargePoint();
            ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);
            cp.ServerCallHandler = _ => ChargePointSimulator.NoAnswer;

            Task<HttpResponseMessage> reset = _client.GetAsync($"/API/Reset/{chargePointId}", TestContext.Current.CancellationToken);
            await cp.WaitForServerCallAsync("Reset");
            Task<HttpResponseMessage> unlock = _client.GetAsync($"/API/UnlockConnector/{chargePointId}/1", TestContext.Current.CancellationToken);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            await cp.DisposeAsync();

            Assert.Equal("Disconnected", await StatusAsync(reset));
            Assert.Equal("Disconnected", await StatusAsync(unlock));
            Assert.Single(cp.ReceivedCalls);

            // new connection => requests are possible immediately
            await using ChargePointSimulator cp2 = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);
            Assert.Equal("Accepted", await StatusAsync(_client.GetAsync($"/API/Reset/{chargePointId}", TestContext.Current.CancellationToken)));
        }

        [Fact]
        public async Task CallerAbortsWhileWaiting_RequestIsNotSent()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp201);
            cp.ServerCallHandler = _ => ChargePointSimulator.NoAnswer;

            Task<HttpResponseMessage> reset = _client.GetAsync($"/API/Reset/{cp.ChargePointId}", TestContext.Current.CancellationToken);
            ServerCall resetCall = await cp.WaitForServerCallAsync("Reset");

            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300)))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => _client.GetAsync($"/API/UnlockConnector/{cp.ChargePointId}/1", cts.Token));
            }

            await AnswerAsync(cp, resetCall, new { status = "Accepted" });
            Assert.Equal("Accepted", await StatusAsync(reset));

            await Task.Delay(500, TestContext.Current.CancellationToken);
            Assert.Single(cp.ReceivedCalls);
        }

        [Fact]
        public async Task CallerAbortsAfterSending_NextRequestWaitsForTheAnswer()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp201);
            cp.ServerCallHandler = _ => ChargePointSimulator.NoAnswer;

            ServerCall resetCall;
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Task<HttpResponseMessage> reset = _client.GetAsync($"/API/Reset/{cp.ChargePointId}", cts.Token);
                resetCall = await cp.WaitForServerCallAsync("Reset");
                cts.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reset);
            }

            // the charge point still processes the first request => the second one has to wait
            Task<HttpResponseMessage> unlock = _client.GetAsync($"/API/UnlockConnector/{cp.ChargePointId}/1", TestContext.Current.CancellationToken);
            await Task.Delay(500, TestContext.Current.CancellationToken);
            Assert.Single(cp.ReceivedCalls);

            await AnswerAsync(cp, resetCall, new { status = "Accepted" });
            ServerCall unlockCall = await cp.WaitForServerCallAsync("UnlockConnector");
            await AnswerAsync(cp, unlockCall, new { status = "Unlocked" });
            Assert.Equal(HttpStatusCode.OK, (await unlock).StatusCode);
        }

        private static Task AnswerAsync(ChargePointSimulator cp, ServerCall call, object payload)
        {
            return cp.SendTextAsync(JsonConvert.SerializeObject(new object[] { 3, call.UniqueId, payload }));
        }

        private static async Task<string?> StatusAsync(Task<HttpResponseMessage> response)
        {
            HttpResponseMessage r = await response;
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            return JObject.Parse(await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))["status"]?.Value<string>();
        }
    }
}
