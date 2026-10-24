using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using Newtonsoft.Json.Linq;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// A new connection of a chargepoint replaces its existing connection ("ReplaceExistingConnection": true = default),
    /// e.g. after a network failure when the old connection still seems to be open.
    /// </summary>
    public class ConnectionReplaceTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public ConnectionReplaceTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task NewConnection_ReplacesOpenConnection(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator oldConnection = await _factory.ConnectAsync(chargePointId, protocol);
            await oldConnection.CallAsync("Heartbeat", new { });

            // old connection is still open (from the view of the server) => new connection is accepted anyway
            await using ChargePointSimulator newConnection = await _factory.ConnectAsync(chargePointId, protocol);

            OcppResult heartbeat = await newConnection.CallAsync("Heartbeat", new { });
            Assert.False(heartbeat.IsError);
            // the old connection is aborted by the server => no more answers
            // (the in-memory WebSocket of the TestServer doesn't show the abort in its client state)
            await Assert.ThrowsAnyAsync<Exception>(() => oldConnection.CallAsync("Heartbeat", new { }, TimeSpan.FromSeconds(2)));
        }

        [Fact]
        public async Task ServerCommands_GoToNewConnection()
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator oldConnection = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);
            await using ChargePointSimulator newConnection = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);
            HttpClient client = _factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync($"/API/Reset/{chargePointId}", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(newConnection.ReceivedCalls, c => c.Action == "Reset");
            Assert.DoesNotContain(oldConnection.ReceivedCalls, c => c.Action == "Reset");
        }

        [Fact]
        public async Task Status_ContainsChargePointOnlyOnce()
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator oldConnection = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);
            await using ChargePointSimulator newConnection = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp21);
            HttpClient client = _factory.CreateClient();

            HttpResponseMessage response = await client.GetAsync("/API/Status", TestContext.Current.CancellationToken);

            JArray status = JArray.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            JToken entry = Assert.Single(status, s => s["id"]?.Value<string>() == chargePointId);
            Assert.Equal(OcppProtocols.Ocpp21, entry["protocol"]!.Value<string>());
        }

        [Fact]
        public async Task PendingApiRequestOnOldConnection_EndsImmediately()
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator oldConnection = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);
            // dead connection: doesn't answer
            oldConnection.ServerCallHandler = call => ChargePointSimulator.NoAnswer;
            HttpClient client = _factory.CreateClient();

            Stopwatch stopwatch = Stopwatch.StartNew();
            Task<HttpResponseMessage> resetTask = client.GetAsync($"/API/Reset/{chargePointId}", TestContext.Current.CancellationToken);
            await oldConnection.WaitForServerCallAsync("Reset");

            await using ChargePointSimulator newConnection = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);
            HttpResponseMessage response = await resetTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

            // without abort the API would wait for the timeout (60 s)
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal("Disconnected", result["status"]!.Value<string>());
        }

        [Fact]
        public async Task OldConnectionClosed_DoesNotRemoveNewConnection()
        {
            string chargePointId = _factory.AddChargePoint();
            ChargePointSimulator oldConnection = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);
            await using ChargePointSimulator newConnection = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);
            await oldConnection.DisposeAsync();
            await Task.Delay(200, TestContext.Current.CancellationToken);

            HttpClient client = _factory.CreateClient();
            HttpResponseMessage response = await client.GetAsync($"/API/Reset/{chargePointId}", TestContext.Current.CancellationToken);

            // still online via the new connection
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(newConnection.ReceivedCalls, c => c.Action == "Reset");
        }
    }

    /// <summary>
    /// Server with "ReplaceExistingConnection": false (old behavior)
    /// </summary>
    public class NoReplaceConnectionServerFactory : OcppServerFactory
    {
        protected override void ConfigureSettings(Dictionary<string, string?> settings)
        {
            settings["ReplaceExistingConnection"] = "false";
        }
    }

    public class ConnectionNoReplaceTests : IClassFixture<NoReplaceConnectionServerFactory>
    {
        private readonly NoReplaceConnectionServerFactory _factory;

        public ConnectionNoReplaceTests(NoReplaceConnectionServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task NewConnection_IsRefusedWithConflict()
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator firstConnection = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);

            InvalidOperationException exp = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16));

            Assert.Contains("409", exp.Message);
            // first connection keeps working
            OcppResult heartbeat = await firstConnection.CallAsync("Heartbeat", new { });
            Assert.False(heartbeat.IsError);
            Assert.Equal(WebSocketState.Open, firstConnection.State);
        }

        [Fact]
        public async Task NewConnection_AfterClose_IsAccepted()
        {
            string chargePointId = _factory.AddChargePoint();
            ChargePointSimulator firstConnection = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);
            await firstConnection.DisposeAsync();

            ChargePointSimulator? second = null;
            await Eventually.TrueAsync(() =>
            {
                try
                {
                    second = _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16).GetAwaiter().GetResult();
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            });

            Assert.NotNull(second);
            await using (second)
            {
                Assert.Equal(WebSocketState.Open, second.State);
            }
        }
    }
}
