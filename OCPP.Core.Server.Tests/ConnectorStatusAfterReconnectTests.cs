using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Every connection starts with an empty live status, but charge points send a StatusNotification only after
    /// a boot or a change. After a reconnect (without boot) the live status must not be "Undefined":
    /// the last known status comes from the database, the current one is requested with TriggerMessage.
    /// </summary>
    public class ConnectorStatusAfterReconnectTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public ConnectorStatusAfterReconnectTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task Reconnect_LiveStatusIsLastKnownStatus(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            await using (ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol))
            {
                OcppResult result = await cp.CallAsync("StatusNotification", StatusNotification(protocol, 2, faulted: true));
                Assert.False(result.IsError);
            }
            // the old connection must be gone - otherwise the status API would still show its live status
            Assert.True(await Eventually.TrueAsync(() => GetOnlineConnectors(chargePointId) == null), "Old connection still registered");

            // reconnect without boot and without new StatusNotification
            await using ChargePointSimulator reconnected = await _factory.ConnectAsync(chargePointId, protocol);

            JObject? connectors = null;
            bool found = await Eventually.TrueAsync(() =>
            {
                connectors = GetOnlineConnectors(chargePointId);
                return connectors?["2"] != null;
            });
            Assert.True(found, "Connector 2 not in the live status after the reconnect");
            Assert.Equal((int)ConnectorStatusEnum.Faulted, connectors!["2"]!["Status"]!.Value<int>());
        }

        [Fact]
        public async Task Connect_UndefinedOrUnknownDbStatus_IsNotLoaded()
        {
            string chargePointId = _factory.AddChargePoint();
            AddConnectorStatus(chargePointId, 1, "Undefined");
            AddConnectorStatus(chargePointId, 2, "SomethingElse");
            AddConnectorStatus(chargePointId, 3, "available");

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);

            JObject? connectors = null;
            Assert.True(await Eventually.TrueAsync(() =>
            {
                connectors = GetOnlineConnectors(chargePointId);
                return connectors?["3"] != null;
            }));
            Assert.Null(connectors!["1"]);
            Assert.Null(connectors["2"]);
            Assert.Equal((int)ConnectorStatusEnum.Available, connectors["3"]!["Status"]!.Value<int>());
        }

        private object StatusNotification(string protocol, int connectorId, bool faulted)
        {
            if (protocol == OcppProtocols.Ocpp16)
            {
                return new { connectorId, errorCode = faulted ? "OtherError" : "NoError", status = faulted ? "Faulted" : "Available", timestamp = DateTimeOffset.UtcNow };
            }
            return new { timestamp = DateTimeOffset.UtcNow, connectorStatus = faulted ? "Faulted" : "Available", evseId = connectorId, connectorId = 1 };
        }

        private void AddConnectorStatus(string chargePointId, int connectorId, string status)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            db.ConnectorStatuses.Add(new ConnectorStatus() { ChargePointId = chargePointId, ConnectorId = connectorId, LastStatus = status, LastStatusTime = DateTime.UtcNow });
            db.SaveChanges();
        }

        /// <summary>
        /// Online connectors of the charge point from the status API (null if not connected)
        /// </summary>
        private JObject? GetOnlineConnectors(string chargePointId)
        {
            HttpClient client = _factory.CreateClient();
            string content = client.GetStringAsync("/API/Status").GetAwaiter().GetResult();
            JToken? chargePoint = JArray.Parse(content).FirstOrDefault(cp => cp["id"]?.Value<string>() == chargePointId);
            return chargePoint?["OnlineConnectors"] as JObject;
        }
    }

    /// <summary>
    /// Server that requests the status 1 second after the connect
    /// </summary>
    public class TriggerStatusServerFactory : OcppServerFactory
    {
        protected override void ConfigureSettings(Dictionary<string, string?> settings)
        {
            settings["TriggerStatusAfterConnect"] = "1";
        }
    }

    public class TriggerStatusAfterConnectTests : IClassFixture<TriggerStatusServerFactory>
    {
        private readonly TriggerStatusServerFactory _factory;

        public TriggerStatusAfterConnectTests(TriggerStatusServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task Connect_KnownConnectors_StatusIsRequested(string protocol)
        {
            string chargePointId = AddChargePointWithConnectors(1, 2);

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            Assert.True(await Eventually.TrueAsync(() => TriggerCalls(cp).Count == 2, TimeSpan.FromSeconds(10)),
                $"Expected 2 TriggerMessage calls, received {TriggerCalls(cp).Count}");
            List<ServerCall> calls = TriggerCalls(cp);
            Assert.All(calls, call => Assert.Equal("StatusNotification", call.Payload["requestedMessage"]!.Value<string>()));
            if (protocol == OcppProtocols.Ocpp16)
            {
                Assert.Equal(new[] { 1, 2 }, calls.Select(c => c.Payload["connectorId"]!.Value<int>()));
            }
            else
            {
                Assert.Equal(new[] { 1, 2 }, calls.Select(c => c.Payload["evse"]!["id"]!.Value<int>()));
                Assert.All(calls, call => Assert.Equal(1, call.Payload["evse"]!["connectorId"]!.Value<int>()));
            }
        }

        [Fact]
        public async Task Connect_WithBootNotification_NoTrigger()
        {
            string chargePointId = AddChargePointWithConnectors(1);

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);
            OcppResult result = await cp.CallAsync("BootNotification", new { reason = "PowerUp", chargingStation = new { model = "Simulator", vendorName = "Test" } });
            Assert.False(result.IsError);

            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Empty(TriggerCalls(cp));
        }

        [Fact]
        public async Task Connect_TriggerNotImplemented_NoFurtherTriggers()
        {
            string chargePointId = AddChargePointWithConnectors(1, 2, 3);

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);
            // null => CALLERROR "NotImplemented"
            cp.ServerCallHandler = call => null;

            await cp.WaitForServerCallAsync("TriggerMessage");
            await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
            Assert.Single(TriggerCalls(cp));
        }

        [Fact]
        public async Task Connect_NoKnownConnectors_NoTrigger()
        {
            string chargePointId = _factory.AddChargePoint();

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);

            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Empty(TriggerCalls(cp));
        }

        private static List<ServerCall> TriggerCalls(ChargePointSimulator cp)
        {
            return cp.ReceivedCalls.Where(c => c.Action == "TriggerMessage").ToList();
        }

        private string AddChargePointWithConnectors(params int[] connectorIds)
        {
            string chargePointId = _factory.AddChargePoint();
            using OCPPCoreContext db = _factory.CreateDbContext();
            foreach (int connectorId in connectorIds)
            {
                db.ConnectorStatuses.Add(new ConnectorStatus() { ChargePointId = chargePointId, ConnectorId = connectorId, LastStatus = "Available", LastStatusTime = DateTime.UtcNow });
            }
            db.SaveChanges();
            return chargePointId;
        }
    }
}
