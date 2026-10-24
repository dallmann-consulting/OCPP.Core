using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// OCPP 2.x: meter values of a transaction when messages contain no meter value.
    /// "MeterStart" &lt; 0 means "unknown" => the first valid meter value is used as start value.
    /// </summary>
    public class TransactionMeterTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public TransactionMeterTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        public static TheoryData<string> Ocpp2xProtocols => new TheoryData<string>(OcppProtocols.Ocpp201, OcppProtocols.Ocpp21);

        private Transaction LoadTransaction(string uid)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.Transactions.Single(t => t.Uid == uid);
        }

        private static Task<OcppResult> TransactionEventAsync(ChargePointSimulator cp, string eventType, string uid, int seqNo, DateTimeOffset timestamp, string triggerReason, double? meterWh = null)
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
            if (meterWh.HasValue)
            {
                payload["meterValue"] = new object[]
                {
                    new { timestamp, sampledValue = new[] { new { value = meterWh.Value, measurand = "Energy.Active.Import.Register", unitOfMeasure = new { unit = "Wh" } } } }
                };
            }
            return cp.CallAsync("TransactionEvent", payload);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task StartedWithoutMeter_FirstUpdatedMeterIsStartValue(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "CablePluggedIn");
            Assert.True(LoadTransaction(uid).MeterStart < 0);

            await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(1), "MeterValuePeriodic", 2000);
            await TransactionEventAsync(cp, "Updated", uid, 2, start.AddMinutes(10), "MeterValuePeriodic", 3500);
            await TransactionEventAsync(cp, "Ended", uid, 3, start.AddMinutes(30), "EVCommunicationLost", 5000);

            Transaction transaction = LoadTransaction(uid);
            Assert.Equal(2.0, transaction.MeterStart);
            Assert.Equal(5.0, transaction.MeterStop);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task StartedWithoutMeter_OnlyEndedWithMeter_EnergyIsZero(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "CablePluggedIn");
            await TransactionEventAsync(cp, "Ended", uid, 1, start.AddMinutes(30), "EVCommunicationLost", 5000);

            // no meter value before the end => energy can't be determined, at least no wrong value (-1)
            Transaction transaction = LoadTransaction(uid);
            Assert.Equal(5.0, transaction.MeterStart);
            Assert.Equal(5.0, transaction.MeterStop);
        }

        /// <summary>
        /// Some charging stations start every transaction with meter 0 => 0 is a valid start value, not "unknown"
        /// </summary>
        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task StartedWithMeterZero_ZeroIsKeptAsStartValue(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "Authorized", 0);
            await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(10), "MeterValuePeriodic", 1500);
            await TransactionEventAsync(cp, "Ended", uid, 2, start.AddMinutes(30), "EVCommunicationLost", 4000);

            Transaction transaction = LoadTransaction(uid);
            Assert.Equal(0.0, transaction.MeterStart);
            Assert.Equal(4.0, transaction.MeterStop);
        }

        [Fact]
        public async Task Ocpp16_StartWithMeterZero_ZeroIsKeptAsStartValue()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            OcppResult startResult = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 0, timestamp = start });
            int transactionId = startResult.Payload!["transactionId"]!.Value<int>();
            await cp.CallAsync("StopTransaction", new { transactionId, idTag = tagId, meterStop = 4000, timestamp = start.AddMinutes(30), reason = "Local" });

            using OCPPCoreContext db = _factory.CreateDbContext();
            Transaction transaction = db.Transactions.Find(transactionId)!;
            Assert.Equal(0.0, transaction.MeterStart);
            Assert.Equal(4.0, transaction.MeterStop);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task StartedWithMeter_UpdatedWithoutMeter_KeepsValues(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "CablePluggedIn", 1000);
            await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(10), "MeterValuePeriodic", 3000);
            await TransactionEventAsync(cp, "Updated", uid, 2, start.AddMinutes(11), "ChargingStateChanged");

            Transaction transaction = LoadTransaction(uid);
            Assert.Equal(1.0, transaction.MeterStart);
            Assert.Equal(3.0, transaction.MeterStop);
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task StartedWithoutMeter_Superseded_UsesMeterOfNextTransaction(string protocol)
        {
            string firstUid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddHours(-2);

            await TransactionEventAsync(cp, "Started", firstUid, 0, start, "CablePluggedIn");
            // "Ended" of the first transaction got lost
            await TransactionEventAsync(cp, "Started", Guid.NewGuid().ToString(), 5, start.AddHours(1), "CablePluggedIn", 8000);

            Transaction transaction = LoadTransaction(firstUid);
            Assert.Equal(TransactionStatus.StopReasonSuperseded, transaction.StopReason);
            Assert.Equal(8.0, transaction.MeterStart);
            Assert.Equal(8.0, transaction.MeterStop);
        }
    }
}
