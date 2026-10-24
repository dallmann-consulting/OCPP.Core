using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Transaction-related messages are only answered with a CALLERROR if a repetition can help
    /// (e.g. database error). Otherwise the charging station would repeat the message and finally discard it.
    /// </summary>
    public class TransactionMessageHandlingTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public TransactionMessageHandlingTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        public static TheoryData<string> Ocpp2xProtocols => new TheoryData<string>(OcppProtocols.Ocpp201, OcppProtocols.Ocpp21);

        private Transaction? LoadTransaction(Func<Transaction, bool> filter)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.Transactions.AsEnumerable().Where(filter).OrderByDescending(t => t.TransactionId).FirstOrDefault();
        }

        private static Task<OcppResult> TransactionEventAsync(ChargePointSimulator cp, string eventType, string uid, int seqNo, DateTimeOffset timestamp, string triggerReason, string? tagId = null, double? meterWh = null)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>
            {
                ["eventType"] = eventType,
                ["timestamp"] = timestamp,
                ["triggerReason"] = triggerReason,
                ["seqNo"] = seqNo,
                ["transactionInfo"] = new { transactionId = uid },
                ["evse"] = new { id = 1, connectorId = 1 }
            };
            if (tagId != null) payload["idToken"] = new { idToken = tagId, type = "ISO14443" };
            if (meterWh.HasValue)
            {
                payload["meterValue"] = new object[]
                {
                    new { timestamp, sampledValue = new[] { new { value = meterWh.Value, measurand = "Energy.Active.Import.Register", unitOfMeasure = new { unit = "Wh" } } } }
                };
            }
            return cp.CallAsync("TransactionEvent", payload);
        }

        // ---------- OCPP 1.6 ----------

        [Fact]
        public async Task Ocpp16_RepeatedStopTransaction_IsAcceptedAndDataUnchanged()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);
            OcppResult startResult = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = start });
            int transactionId = startResult.Payload!["transactionId"]!.Value<int>();

            await cp.CallAsync("StopTransaction", new { transactionId, idTag = tagId, meterStop = 5000, timestamp = start.AddMinutes(20), reason = "Local" });
            // repeated (e.g. response got lost) - with other values to check that nothing is overwritten
            OcppResult repeated = await cp.CallAsync("StopTransaction", new { transactionId, idTag = tagId, meterStop = 9999, timestamp = start.AddMinutes(25), reason = "Local" });

            Assert.False(repeated.IsError);
            Transaction transaction = LoadTransaction(t => t.TransactionId == transactionId)!;
            Assert.Equal(5.0, transaction.MeterStop);
            Assert.Equal(start.AddMinutes(20).UtcDateTime, transaction.StopTime!.Value, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Ocpp16_LateStopAfterSuperseded_TakesOverRealValues()
        {
            string tagId = _factory.AddChargeTag();
            string otherTagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddHours(-3);

            OcppResult first = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = start });
            int firstId = first.Payload!["transactionId"]!.Value<int>();
            // next transaction starts before the stop of the first one arrives => first one is closed automatically
            await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = otherTagId, meterStart = 8000, timestamp = start.AddHours(2) });
            Assert.Equal(TransactionStatus.StopReasonSuperseded, LoadTransaction(t => t.TransactionId == firstId)!.StopReason);

            // the real stop of the first transaction arrives late
            OcppResult lateStop = await cp.CallAsync("StopTransaction", new { transactionId = firstId, idTag = tagId, meterStop = 7500, timestamp = start.AddHours(1), reason = "Local" });

            Assert.False(lateStop.IsError);
            Transaction transaction = LoadTransaction(t => t.TransactionId == firstId)!;
            Assert.Equal(7.5, transaction.MeterStop);
            Assert.Equal(start.AddHours(1).UtcDateTime, transaction.StopTime!.Value, TimeSpan.FromSeconds(1));
            Assert.Equal("Local", transaction.StopReason);
            Assert.Equal(tagId, transaction.StopTagId);
        }

        [Fact]
        public async Task Ocpp16_StopForTransactionOfOtherChargePoint_IsAcceptedAndNotChanged()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp1 = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            await using ChargePointSimulator cp2 = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            OcppResult start = await cp1.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = DateTimeOffset.UtcNow.AddMinutes(-10) });
            int transactionId = start.Payload!["transactionId"]!.Value<int>();

            OcppResult stop = await cp2.CallAsync("StopTransaction", new { transactionId, meterStop = 5000, timestamp = DateTimeOffset.UtcNow, reason = "Local" });

            Assert.False(stop.IsError);
            Assert.Null(LoadTransaction(t => t.TransactionId == transactionId)!.StopTime);
        }

        // ---------- OCPP 2.x ----------

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_RepeatedEnded_IsAcceptedAndDataUnchanged(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "Authorized", tagId, 1000);
            await TransactionEventAsync(cp, "Ended", uid, 1, start.AddMinutes(20), "StopAuthorized", tagId, 5000);
            OcppResult repeated = await TransactionEventAsync(cp, "Ended", uid, 1, start.AddMinutes(25), "StopAuthorized", tagId, 9999);

            Assert.False(repeated.IsError);
            Transaction transaction = LoadTransaction(t => t.Uid == uid)!;
            Assert.Equal(5.0, transaction.MeterStop);
            Assert.Equal(start.AddMinutes(20).UtcDateTime, transaction.StopTime!.Value, TimeSpan.FromSeconds(1));
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_LateEndedAfterSuperseded_TakesOverRealValues(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            string firstUid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddHours(-3);

            await TransactionEventAsync(cp, "Started", firstUid, 0, start, "Authorized", tagId, 1000);
            await TransactionEventAsync(cp, "Started", Guid.NewGuid().ToString(), 5, start.AddHours(2), "Authorized", tagId, 8000);
            Assert.Equal(TransactionStatus.StopReasonSuperseded, LoadTransaction(t => t.Uid == firstUid)!.StopReason);

            OcppResult lateEnded = await TransactionEventAsync(cp, "Ended", firstUid, 1, start.AddHours(1), "StopAuthorized", tagId, 7500);

            Assert.False(lateEnded.IsError);
            Transaction transaction = LoadTransaction(t => t.Uid == firstUid)!;
            Assert.Equal(7.5, transaction.MeterStop);
            Assert.Equal(start.AddHours(1).UtcDateTime, transaction.StopTime!.Value, TimeSpan.FromSeconds(1));
            Assert.NotEqual(TransactionStatus.StopReasonSuperseded, transaction.StopReason);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_UpdatedAfterEnded_IsAcceptedAndDataUnchanged(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "Authorized", tagId, 1000);
            await TransactionEventAsync(cp, "Ended", uid, 1, start.AddMinutes(20), "StopAuthorized", tagId, 5000);
            OcppResult updated = await TransactionEventAsync(cp, "Updated", uid, 2, start.AddMinutes(21), "MeterValuePeriodic", meterWh: 6000);

            Assert.False(updated.IsError);
            Assert.Equal(5.0, LoadTransaction(t => t.Uid == uid)!.MeterStop);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_EndedForUnknownTransaction_IsAcceptedWithoutTransaction(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            OcppResult ended = await TransactionEventAsync(cp, "Ended", uid, 3, DateTimeOffset.UtcNow, "EVCommunicationLost", meterWh: 5000);

            Assert.False(ended.IsError);
            Assert.Null(LoadTransaction(t => t.Uid == uid));
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_StartedLost_TransactionIsCreatedFromUpdatedAndClosed(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            // "Started" (with meter 1000 Wh) got lost => first message is an "Updated"
            await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(5), "MeterValuePeriodic", tagId, 2000);
            OcppResult ended = await TransactionEventAsync(cp, "Ended", uid, 2, start.AddMinutes(30), "StopAuthorized", tagId, 9000);

            Assert.False(ended.IsError);
            Transaction transaction = LoadTransaction(t => t.Uid == uid)!;
            Assert.Equal(tagId, transaction.StartTagId);
            Assert.Equal(2.0, transaction.MeterStart);
            Assert.Equal(9.0, transaction.MeterStop);
            Assert.NotNull(transaction.StopTime);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_EndedWithoutMeterValue_KeepsLastMeterValue(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "Authorized", tagId, 1000);
            await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(10), "MeterValuePeriodic", meterWh: 4000);
            await TransactionEventAsync(cp, "Ended", uid, 2, start.AddMinutes(20), "EVCommunicationLost");

            Transaction transaction = LoadTransaction(t => t.Uid == uid)!;
            Assert.NotNull(transaction.StopTime);
            Assert.Equal(4.0, transaction.MeterStop);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_SameTransactionIdOnTwoChargingStations_AreSeparated(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            // some charging stations simply count their transaction ids
            string uid = "1" + Guid.NewGuid().ToString("N").Substring(0, 6);
            await using ChargePointSimulator cp1 = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            await using ChargePointSimulator cp2 = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp1, "Started", uid, 0, start, "Authorized", tagId, 1000);
            await TransactionEventAsync(cp2, "Started", uid, 0, start.AddMinutes(1), "Authorized", tagId, 3000);
            OcppResult ended1 = await TransactionEventAsync(cp1, "Ended", uid, 1, start.AddMinutes(20), "StopAuthorized", tagId, 5000);

            Assert.False(ended1.IsError);
            Transaction transaction1 = LoadTransaction(t => t.Uid == uid && t.ChargePointId == cp1.ChargePointId)!;
            Transaction transaction2 = LoadTransaction(t => t.Uid == uid && t.ChargePointId == cp2.ChargePointId)!;
            Assert.NotNull(transaction1.StopTime);
            Assert.Equal(5.0, transaction1.MeterStop);
            Assert.Null(transaction2.StopTime);
        }
    }
}
