using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Transactions that don't follow the normal flow:
    /// started offline with a token that is invalid at the server, started without token,
    /// stopped without token, stop message lost.
    /// Every transaction must be recorded and closed again.
    /// </summary>
    public class TransactionScenarioTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public TransactionScenarioTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        public static TheoryData<string> Ocpp2xProtocols => new TheoryData<string>(OcppProtocols.Ocpp201, OcppProtocols.Ocpp21);

        private Transaction? LoadTransaction(Func<Transaction, bool> filter)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.Transactions.AsEnumerable().Where(filter).OrderByDescending(t => t.TransactionId).FirstOrDefault();
        }

        private static object IdToken(string tagId) => new { idToken = tagId, type = "ISO14443" };

        private static object[] Meter(DateTimeOffset timestamp, double wh) => new object[]
        {
            new { timestamp, sampledValue = new[] { new { value = wh, measurand = "Energy.Active.Import.Register", unitOfMeasure = new { unit = "Wh" } } } }
        };

        private static Task<OcppResult> TransactionEventAsync(ChargePointSimulator cp, string eventType, string uid, int seqNo, DateTimeOffset timestamp, string triggerReason, string? tagId = null, double? meterWh = null, string? stoppedReason = null, bool offline = false)
        {
            Dictionary<string, object> transactionInfo = new Dictionary<string, object> { ["transactionId"] = uid };
            if (stoppedReason != null) transactionInfo["stoppedReason"] = stoppedReason;

            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                ["eventType"] = eventType,
                ["timestamp"] = timestamp,
                ["triggerReason"] = triggerReason,
                ["seqNo"] = seqNo,
                ["transactionInfo"] = transactionInfo,
                ["evse"] = new { id = 1, connectorId = 1 }
            };
            if (offline) payload["offline"] = true;
            if (tagId != null) payload["idToken"] = IdToken(tagId);
            if (meterWh.HasValue) payload["meterValue"] = Meter(timestamp, meterWh.Value);
            return cp.CallAsync("TransactionEvent", payload);
        }

        // ---------- started offline, token invalid at the server ----------

        [Theory]
        [InlineData("unknown")]
        [InlineData("blocked")]
        public async Task Ocpp16_OfflineStartWithInvalidTag_IsRecordedAndClosed(string tagKind)
        {
            string tagId = (tagKind == "blocked") ? _factory.AddChargeTag(blocked: true) : "UNKNOWN" + Guid.NewGuid().ToString("N").Substring(0, 8);
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-20);

            // StartTransaction is delivered after the reconnect (timestamp in the past)
            OcppResult startResult = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = start });
            Assert.False(startResult.IsError);
            string status = startResult.Payload!["idTagInfo"]!["status"]!.Value<string>()!;
            Assert.Equal((tagKind == "blocked") ? "Blocked" : "Invalid", status);
            int transactionId = startResult.Payload["transactionId"]!.Value<int>();
            Assert.True(transactionId > 0);

            // the charge point stops the transaction (StopTransactionOnInvalidId = true => reason DeAuthorized)
            OcppResult stopResult = await cp.CallAsync("StopTransaction", new { transactionId, idTag = tagId, meterStop = 6000, timestamp = start.AddMinutes(15), reason = "DeAuthorized" });
            Assert.False(stopResult.IsError);

            Transaction? transaction = LoadTransaction(t => t.TransactionId == transactionId);
            Assert.NotNull(transaction);
            Assert.Equal(tagId, transaction.StartTagId);
            Assert.Equal(status, transaction.StartResult);
            Assert.NotNull(transaction.StopTime);
            Assert.Equal(6.0, transaction.MeterStop);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_OfflineStartWithUnknownToken_IsRecordedAndClosed(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-40);

            OcppResult started = await TransactionEventAsync(cp, "Started", uid, 0, start, "Authorized", tagId: "UNKNOWNTOKEN", meterWh: 1000, offline: true);
            Assert.False(started.IsError);
            Assert.NotEqual("Accepted", started.Payload!["idTokenInfo"]!["status"]!.Value<string>());

            OcppResult ended = await TransactionEventAsync(cp, "Ended", uid, 1, start.AddMinutes(30), "Deauthorized", meterWh: 5000, stoppedReason: "DeAuthorized", offline: true);
            Assert.False(ended.IsError);

            Transaction? transaction = LoadTransaction(t => t.Uid == uid);
            Assert.NotNull(transaction);
            Assert.Equal("UNKNOWNTOKEN", transaction.StartTagId);
            Assert.True(TransactionStatus.IsRejectedStartResult(transaction.StartResult));
            Assert.NotNull(transaction.StopTime);
            Assert.Equal(5.0, transaction.MeterStop);
        }

        // ---------- OCPP 2.x: cable plugged in first (started without token) ----------

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_CableFirst_TokenLater_IsRecordedWithTag(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            OcppResult started = await TransactionEventAsync(cp, "Started", uid, 0, start, "CablePluggedIn", meterWh: 1000);
            Assert.False(started.IsError);
            // no token in the request => no idTokenInfo in the response
            Assert.Null(started.Payload!["idTokenInfo"]);
            Transaction? transaction = LoadTransaction(t => t.Uid == uid);
            Assert.NotNull(transaction);
            Assert.Null(transaction.StartTagId);

            OcppResult authorized = await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(1), "Authorized", tagId: tagId);
            Assert.False(authorized.IsError);
            Assert.Equal("Accepted", authorized.Payload!["idTokenInfo"]!["status"]!.Value<string>());
            transaction = LoadTransaction(t => t.Uid == uid)!;
            Assert.Equal(tagId, transaction.StartTagId);
            Assert.Equal("Accepted", transaction.StartResult);

            OcppResult ended = await TransactionEventAsync(cp, "Ended", uid, 2, start.AddMinutes(30), "EVCommunicationLost", meterWh: 9000, stoppedReason: "EVDisconnected");
            Assert.False(ended.IsError);

            transaction = LoadTransaction(t => t.Uid == uid)!;
            Assert.NotNull(transaction.StopTime);
            Assert.Equal(9.0, transaction.MeterStop);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_CableFirst_NeverAuthorized_IsClosedWithoutTag(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-5);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "CablePluggedIn", meterWh: 1000);
            OcppResult ended = await TransactionEventAsync(cp, "Ended", uid, 1, start.AddMinutes(2), "EVConnectTimeout", meterWh: 1000, stoppedReason: "Timeout");
            Assert.False(ended.IsError);

            Transaction? transaction = LoadTransaction(t => t.Uid == uid);
            Assert.NotNull(transaction);
            Assert.NotNull(transaction.StopTime);
            Assert.Null(transaction.StartTagId);
            Assert.Null(transaction.StartResult);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_RepeatedStarted_CreatesOnlyOneTransaction(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-5);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "Authorized", tagId: tagId, meterWh: 1000);
            OcppResult repeated = await TransactionEventAsync(cp, "Started", uid, 0, start, "Authorized", tagId: tagId, meterWh: 1000);

            Assert.False(repeated.IsError);
            using OCPPCoreContext db = _factory.CreateDbContext();
            Transaction transaction = Assert.Single(db.Transactions.Where(t => t.Uid == uid));
            Assert.Null(transaction.StopTime);
        }

        // ---------- stop without tag (EV unplugged) for a start tag with group ----------

        [Fact]
        public async Task Ocpp16_StopWithoutTag_StartTagWithGroup_IsClosed()
        {
            string parentTag = _factory.AddChargeTag();
            string tagId = _factory.AddChargeTag(parentTagId: parentTag);
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            OcppResult startResult = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = start });
            int transactionId = startResult.Payload!["transactionId"]!.Value<int>();
            OcppResult stopResult = await cp.CallAsync("StopTransaction", new { transactionId, meterStop = 5000, timestamp = start.AddMinutes(30), reason = "EVDisconnected" });

            Assert.False(stopResult.IsError);
            Assert.Equal("Accepted", stopResult.Payload!["idTagInfo"]!["status"]!.Value<string>());
            Transaction transaction = LoadTransaction(t => t.TransactionId == transactionId)!;
            Assert.NotNull(transaction.StopTime);
            Assert.Equal(5.0, transaction.MeterStop);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_EndedWithoutToken_StartTagWithGroup_IsClosed(string protocol)
        {
            string parentTag = _factory.AddChargeTag();
            string tagId = _factory.AddChargeTag(parentTagId: parentTag);
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "Authorized", tagId: tagId, meterWh: 1000);
            OcppResult ended = await TransactionEventAsync(cp, "Ended", uid, 1, start.AddMinutes(30), "EVCommunicationLost", meterWh: 5000, stoppedReason: "EVDisconnected");

            Assert.False(ended.IsError);
            Transaction transaction = LoadTransaction(t => t.Uid == uid)!;
            Assert.NotNull(transaction.StopTime);
            Assert.Equal(5.0, transaction.MeterStop);
        }

        // ---------- stop message lost => next start on the same connector closes the old transaction ----------

        [Fact]
        public async Task Ocpp16_StopLost_NextStartClosesOldTransaction()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddHours(-3);

            OcppResult first = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = start });
            int firstId = first.Payload!["transactionId"]!.Value<int>();
            // StopTransaction for the first transaction never arrives
            OcppResult second = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 8000, timestamp = start.AddHours(2) });
            int secondId = second.Payload!["transactionId"]!.Value<int>();

            Transaction firstTransaction = LoadTransaction(t => t.TransactionId == firstId)!;
            Assert.Equal(start.AddHours(2).UtcDateTime, firstTransaction.StopTime!.Value, TimeSpan.FromSeconds(1));
            Assert.Equal(8.0, firstTransaction.MeterStop);
            Assert.Equal(TransactionStatus.StopReasonSuperseded, firstTransaction.StopReason);
            Assert.Null(LoadTransaction(t => t.TransactionId == secondId)!.StopTime);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_EndedLost_NextStartClosesOldTransaction(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            string firstUid = Guid.NewGuid().ToString();
            string secondUid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddHours(-3);

            await TransactionEventAsync(cp, "Started", firstUid, 0, start, "Authorized", tagId: tagId, meterWh: 1000);
            await TransactionEventAsync(cp, "Updated", firstUid, 1, start.AddMinutes(30), "MeterValuePeriodic", meterWh: 4000);
            // "Ended" for the first transaction never arrives
            await TransactionEventAsync(cp, "Started", secondUid, 5, start.AddHours(2), "Authorized", tagId: tagId, meterWh: 8000);

            Transaction firstTransaction = LoadTransaction(t => t.Uid == firstUid)!;
            Assert.NotNull(firstTransaction.StopTime);
            // last known meter value of the transaction is kept
            Assert.Equal(4.0, firstTransaction.MeterStop);
            Assert.Equal(TransactionStatus.StopReasonSuperseded, firstTransaction.StopReason);
            Assert.Null(LoadTransaction(t => t.Uid == secondUid)!.StopTime);
        }

        [Fact]
        public async Task NewStart_OnOtherConnector_DoesNotCloseTransaction()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddHours(-1);

            OcppResult first = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = start });
            await cp.CallAsync("StartTransaction", new { connectorId = 2, idTag = tagId, meterStart = 500, timestamp = start.AddMinutes(10) });

            Assert.Null(LoadTransaction(t => t.TransactionId == first.Payload!["transactionId"]!.Value<int>())!.StopTime);
        }
    }
}
