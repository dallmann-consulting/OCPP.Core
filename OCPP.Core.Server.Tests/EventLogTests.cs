using System.Net;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Business events of the charge points (EventLog)
    /// </summary>
    public class EventLogTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public EventLogTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        private List<ChargePointEvent> Events(string chargePointId, string? eventCode = null)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.ChargePointEvents.AsNoTracking()
                .Where(e => e.ChargePointId == chargePointId && (eventCode == null || e.EventCode == eventCode))
                .OrderBy(e => e.EventId)
                .ToList();
        }

        private static Dictionary<string, string> Parameters(ChargePointEvent e) => EventLogExtensions.ReadParameters(e.Parameters);

        private async Task<List<ChargePointEvent>> WaitForEventsAsync(string chargePointId, string eventCode, int count = 1)
        {
            // events written asynchronously to the connection (the client is connected before the server writes "Connected",
            // "Disconnected" is written after the connection ended)
            for (int i = 0; i < 100; i++)
            {
                List<ChargePointEvent> events = Events(chargePointId, eventCode);
                if (events.Count >= count) return events;
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
            return Events(chargePointId, eventCode);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task ConnectAndDisconnect_AreLogged(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            ChargePointEvent connected = Assert.Single(await WaitForEventsAsync(chargePointId, EventCode.Connected));
            Assert.Equal(EventCategory.Connection, connected.Category);
            Assert.Equal(EventSeverity.Info, connected.Severity);
            Assert.Equal(protocol, Parameters(connected)["protocol"]);

            await cp.DisposeAsync();
            Assert.Single(await WaitForEventsAsync(chargePointId, EventCode.Disconnected));
        }

        [Fact]
        public async Task WrongPassword_IsLogged()
        {
            string chargePointId = _factory.AddChargePoint(username: "user1", password: ChargePointPassword.Hash("secret-password-123"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16, "user1", "wrong"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16));

            List<ChargePointEvent> events = Events(chargePointId, EventCode.AuthenticationFailed);
            Assert.Equal(new[] { "Password", "Missing" }, events.Select(e => Parameters(e)["method"]));
            Assert.All(events, e => Assert.Equal(EventSeverity.Warning, e.Severity));
            Assert.Empty(Events(chargePointId, EventCode.Connected));
        }

        [Fact]
        public async Task ReplacedConnection_IsLogged()
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator first = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);
            await using ChargePointSimulator second = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);

            Assert.Single(await WaitForEventsAsync(chargePointId, EventCode.ConnectionReplaced));
            Assert.Equal(2, (await WaitForEventsAsync(chargePointId, EventCode.Connected, 2)).Count);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task Boot_FirmwareChangeIsDetected(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            await BootAsync(cp, protocol, "1.0");
            await BootAsync(cp, protocol, "1.0");
            Assert.Empty(Events(chargePointId, EventCode.FirmwareChanged));

            await BootAsync(cp, protocol, "1.1");
            List<ChargePointEvent> boots = Events(chargePointId, EventCode.Boot);
            Assert.Equal(3, boots.Count);
            Assert.Equal("Vendor1", Parameters(boots[0])["vendor"]);
            Assert.Equal("Model1", Parameters(boots[0])["model"]);
            ChargePointEvent changed = Assert.Single(Events(chargePointId, EventCode.FirmwareChanged));
            Assert.Equal("1.0", Parameters(changed)["old"]);
            Assert.Equal("1.1", Parameters(changed)["new"]);
        }

        private static async Task BootAsync(ChargePointSimulator cp, string protocol, string firmware)
        {
            object payload = protocol == OcppProtocols.Ocpp16
                ? new { chargePointVendor = "Vendor1", chargePointModel = "Model1", firmwareVersion = firmware }
                : new { chargingStation = new { vendorName = "Vendor1", model = "Model1", firmwareVersion = firmware }, reason = "PowerUp" };
            OcppResult result = await cp.CallAsync("BootNotification", payload);
            Assert.Equal(3, result.MessageType);
        }

        [Fact]
        public async Task Ocpp16_StatusChangedOnlyOnRealChange()
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);

            await Status16Async(cp, "Available");
            await Status16Async(cp, "Available");
            await Status16Async(cp, "Charging");
            // Charging => SuspendedEV is still "occupied" => no event
            await Status16Async(cp, "SuspendedEV");
            await Status16Async(cp, "Faulted", "GroundFailure");

            List<ChargePointEvent> events = Events(chargePointId, EventCode.StatusChanged);
            Assert.Equal(new[] { "Available", "Occupied", "Faulted" }, events.Select(e => Parameters(e)["new"]));
            Assert.Equal("Occupied", Parameters(events[2])["old"]);
            Assert.Equal("GroundFailure", Parameters(events[2])["errorCode"]);
            Assert.Equal(EventSeverity.Error, events[2].Severity);
            Assert.All(events, e => Assert.Equal(1, e.ConnectorId));
        }

        private static async Task Status16Async(ChargePointSimulator cp, string status, string errorCode = "NoError")
        {
            OcppResult result = await cp.CallAsync("StatusNotification", new { connectorId = 1, errorCode, status, timestamp = DateTimeOffset.UtcNow });
            Assert.Equal(3, result.MessageType);
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task Ocpp2x_StatusChanged(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            foreach (string status in new[] { "Available", "Occupied", "Occupied", "Unavailable" })
            {
                await cp.CallAsync("StatusNotification", new { timestamp = DateTimeOffset.UtcNow, connectorStatus = status, evseId = 1, connectorId = 1 });
            }

            List<ChargePointEvent> events = Events(chargePointId, EventCode.StatusChanged);
            Assert.Equal(new[] { "Available", "Occupied", "Unavailable" }, events.Select(e => Parameters(e)["new"]));
            Assert.Equal(EventSeverity.Warning, events[2].Severity);
        }

        [Fact]
        public async Task Ocpp16_AuthorizationAndTransaction()
        {
            string chargePointId = _factory.AddChargePoint();
            string tagId = _factory.AddChargeTag();
            string blockedTag = _factory.AddChargeTag(blocked: true);
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);

            await cp.CallAsync("Authorize", new { idTag = "UNKNOWNTAG" });
            await cp.CallAsync("Authorize", new { idTag = blockedTag });
            await cp.CallAsync("Authorize", new { idTag = tagId });
            OcppResult start = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = DateTimeOffset.UtcNow });
            int transactionId = start.Payload!["transactionId"]!.Value<int>();
            await cp.CallAsync("StopTransaction", new { transactionId, idTag = tagId, meterStop = 3500, timestamp = DateTimeOffset.UtcNow, reason = "Local" });

            List<ChargePointEvent> rejected = Events(chargePointId, EventCode.TokenRejected);
            Assert.Equal(new[] { "Invalid", "Blocked" }, rejected.Select(e => Parameters(e)["status"]));
            Assert.All(rejected, e => Assert.Equal(EventSeverity.Warning, e.Severity));

            List<ChargePointEvent> accepted = Events(chargePointId, EventCode.TokenAccepted);
            Assert.Equal(new[] { "Authorize", "Start", "Stop" }, accepted.Select(e => Parameters(e)["action"]));
            Assert.All(accepted, e => Assert.Equal(tagId, Parameters(e)["tag"]));

            ChargePointEvent started = Assert.Single(Events(chargePointId, EventCode.TransactionStarted));
            Assert.Equal(transactionId.ToString(), Parameters(started)["transactionId"]);
            Assert.Equal(1, started.ConnectorId);
            ChargePointEvent stopped = Assert.Single(Events(chargePointId, EventCode.TransactionStopped));
            Assert.Equal("2.5", Parameters(stopped)["energy"]);
            Assert.Equal("Local", Parameters(stopped)["reason"]);
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task Ocpp2x_AuthorizationAndTransaction(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            string tagId = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-10);

            await cp.CallAsync("Authorize", new { idToken = new { idToken = "UNKNOWNTAG", type = "ISO14443" } });
            await TransactionEventAsync(cp, "Started", uid, 0, start, 1000, tagId);
            await TransactionEventAsync(cp, "Ended", uid, 1, start.AddMinutes(10), 4000, null);

            ChargePointEvent rejected = Assert.Single(Events(chargePointId, EventCode.TokenRejected));
            Assert.Equal("Authorize", Parameters(rejected)["action"]);
            ChargePointEvent accepted = Assert.Single(Events(chargePointId, EventCode.TokenAccepted));
            Assert.Equal("Start", Parameters(accepted)["action"]);
            Assert.Single(Events(chargePointId, EventCode.TransactionStarted));
            ChargePointEvent stopped = Assert.Single(Events(chargePointId, EventCode.TransactionStopped));
            Assert.Equal("3", Parameters(stopped)["energy"]);
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task Ocpp2x_TokenRepeatedInUpdated_IsLoggedOnlyOnce(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            string tagId = _factory.AddChargeTag();
            string otherTagId = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-10);

            // some stations send the token in every "Updated" (e.g. periodic meter values)
            await TransactionEventAsync(cp, "Started", uid, 0, start, 1000, tagId);
            for (int i = 1; i <= 3; i++)
            {
                OcppResult updated = await TransactionEventAsync(cp, "Updated", uid, i, start.AddMinutes(i), 1000 + i * 100, tagId);
                // the charge point still gets the authorization status
                Assert.Equal("Accepted", updated.Payload!["idTokenInfo"]!["status"]!.Value<string>());
            }
            // another token during the transaction is a new authorization
            await TransactionEventAsync(cp, "Updated", uid, 4, start.AddMinutes(4), 1500, otherTagId);
            await TransactionEventAsync(cp, "Ended", uid, 5, start.AddMinutes(10), 2000, tagId);

            List<ChargePointEvent> accepted = Events(chargePointId, EventCode.TokenAccepted);
            Assert.Equal(new[] { "Start", "Start", "Stop" }, accepted.Select(e => Parameters(e)["action"]));
            Assert.Equal(new[] { tagId, otherTagId, tagId }, accepted.Select(e => Parameters(e)["tag"]));
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201)]
        [InlineData(OcppProtocols.Ocpp21)]
        public async Task Ocpp2x_TokenAfterStartInUpdated_IsLogged(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            string tagId = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-10);

            // cable plugged in first => token follows in "Updated"
            await TransactionEventAsync(cp, "Started", uid, 0, start, 1000, null);
            await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(1), 1100, tagId);
            await TransactionEventAsync(cp, "Updated", uid, 2, start.AddMinutes(2), 1200, tagId);

            ChargePointEvent accepted = Assert.Single(Events(chargePointId, EventCode.TokenAccepted));
            Assert.Equal(tagId, Parameters(accepted)["tag"]);
            Assert.Equal("Start", Parameters(accepted)["action"]);
        }

        private static Task<OcppResult> TransactionEventAsync(ChargePointSimulator cp, string eventType, string uid, int seqNo, DateTimeOffset timestamp, double meterWh, string? idTag)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                ["eventType"] = eventType,
                ["timestamp"] = timestamp,
                ["triggerReason"] = eventType == "Started" ? "Authorized" : "EVCommunicationLost",
                ["seqNo"] = seqNo,
                ["transactionInfo"] = new { transactionId = uid },
                ["evse"] = new { id = 1, connectorId = 1 },
                ["meterValue"] = new object[]
                {
                    new { timestamp, sampledValue = new[] { new { value = meterWh, measurand = "Energy.Active.Import.Register", unitOfMeasure = new { unit = "Wh" } } } }
                }
            };
            if (idTag != null) payload["idToken"] = new { idToken = idTag, type = "ISO14443" };
            return cp.CallAsync("TransactionEvent", payload);
        }

        [Fact]
        public async Task SupersededTransaction_IsLogged()
        {
            string chargePointId = _factory.AddChargePoint();
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);

            await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = DateTimeOffset.UtcNow.AddMinutes(-5) });
            await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 2000, timestamp = DateTimeOffset.UtcNow });

            ChargePointEvent closed = Assert.Single(Events(chargePointId, EventCode.TransactionClosedAutomatically));
            Assert.Equal(EventSeverity.Warning, closed.Severity);
            Assert.Equal("1", Parameters(closed)["energy"]);
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task Command_IsLoggedWithResultAndUser(string protocol)
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);
            HttpClient client = _factory.CreateClient();

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"/API/Reset/{chargePointId}"))
            {
                request.Headers.Add("X-OCPP-User", "admin");
                Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request, TestContext.Current.CancellationToken)).StatusCode);
            }
            // without user => "API", CALLERROR of the charge point => warning
            cp.ServerCallHandler = call => null;
            await client.GetAsync($"/API/UnlockConnector/{chargePointId}/1", TestContext.Current.CancellationToken);

            List<ChargePointEvent> commands = Events(chargePointId, EventCode.Command);
            Assert.Equal(2, commands.Count);
            Assert.Equal("Reset", Parameters(commands[0])["action"]);
            Assert.Equal("Accepted", Parameters(commands[0])["result"]);
            Assert.Equal("admin", Parameters(commands[0])["user"]);
            Assert.Equal(EventSeverity.Info, commands[0].Severity);
            Assert.NotNull(commands[0].Details);

            Assert.Equal("UnlockConnector", Parameters(commands[1])["action"]);
            Assert.Equal("CallError", Parameters(commands[1])["result"]);
            Assert.Equal("API", Parameters(commands[1])["user"]);
            Assert.Equal(1, commands[1].ConnectorId);
            Assert.Equal(EventSeverity.Warning, commands[1].Severity);
        }

        [Fact]
        public async Task ReadingCommand_IsNotLogged()
        {
            string chargePointId = _factory.AddChargePoint();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);
            cp.ServerCallHandler = call => new { configurationKey = new object[0] };

            await _factory.CreateClient().GetAsync($"/API/GetVariables/{chargePointId}", TestContext.Current.CancellationToken);

            Assert.Contains(cp.ReceivedCalls, c => c.Action == "GetConfiguration");
            Assert.Empty(Events(chargePointId, EventCode.Command));
        }

        [Fact]
        public async Task DeleteOldEvents_DeletesOnlyOldEvents()
        {
            string chargePointId = _factory.AddChargePoint();
            using (OCPPCoreContext db = _factory.CreateDbContext())
            {
                for (int i = 0; i < 5; i++)
                {
                    db.ChargePointEvents.Add(new ChargePointEvent { ChargePointId = chargePointId, EventTime = DateTime.UtcNow.AddDays(-400 - i), Category = EventCategory.Status, Severity = EventSeverity.Info, EventCode = EventCode.StatusChanged });
                }
                db.ChargePointEvents.Add(new ChargePointEvent { ChargePointId = chargePointId, EventTime = DateTime.UtcNow, Category = EventCategory.Status, Severity = EventSeverity.Info, EventCode = EventCode.StatusChanged });
                db.SaveChanges();
            }

            using (OCPPCoreContext db = _factory.CreateDbContext())
            {
                int deleted = await LogCleanupService.DeleteOldEventsAsync(db, DateTime.UtcNow.AddDays(-365), 2, CancellationToken.None);
                Assert.Equal(5, deleted);
            }
            Assert.Single(Events(chargePointId));
        }

        [Fact]
        public void DeletingChargePoint_DeletesItsEvents()
        {
            string chargePointId = _factory.AddChargePoint();
            using (OCPPCoreContext db = _factory.CreateDbContext())
            {
                db.WriteEvent(null, chargePointId, null, EventCategory.Station, EventSeverity.Info, EventCode.Boot);
                Assert.Single(db.ChargePointEvents.Where(e => e.ChargePointId == chargePointId));

                db.ChargePoints.Remove(db.ChargePoints.Single(c => c.ChargePointId == chargePointId));
                db.SaveChanges();
            }
            Assert.Empty(Events(chargePointId));
        }
    }
}
