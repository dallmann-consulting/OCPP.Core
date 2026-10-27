using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Meter values smaller than before (within a transaction, e.g. an error of the charge point).
    /// Intermediate values don't lower the meter, final values are taken over unchanged.
    /// Always an EventLog entry ("MeterDecreased").
    /// </summary>
    public class MeterDecreasedTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public MeterDecreasedTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        public static TheoryData<string> Ocpp2xProtocols => new TheoryData<string>(OcppProtocols.Ocpp201, OcppProtocols.Ocpp21);

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_SmallerUpdatedIsIgnored_SmallerEndedIsTakenOver(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, 10000);
            await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(5), 12000);
            Assert.Empty(MeterWarnings(cp.ChargePointId));

            // intermediate value smaller => ignored (highest value is kept)
            await TransactionEventAsync(cp, "Updated", uid, 2, start.AddMinutes(10), 11000);
            Assert.Equal(12.0, LoadTransaction(uid).MeterStop!.Value, 6);
            Assert.Single(MeterWarnings(cp.ChargePointId));

            // final value smaller than the start => taken over unchanged (negative energy is visible)
            await TransactionEventAsync(cp, "Ended", uid, 3, start.AddMinutes(20), 9000);
            Transaction transaction = LoadTransaction(uid);
            Assert.Equal(10.0, transaction.MeterStart, 6);
            Assert.Equal(9.0, transaction.MeterStop!.Value, 6);

            List<ChargePointEvent> warnings = MeterWarnings(cp.ChargePointId);
            Assert.Equal(2, warnings.Count);
            Assert.All(warnings, w => Assert.StartsWith("TransactionEvent:", w.Details));
            Assert.Contains(warnings, w => EventLogExtensions.ReadParameters(w.Parameters)["new"] == "9" && EventLogExtensions.ReadParameters(w.Parameters)["takenOver"] == "true");
            Assert.Contains(warnings, w => EventLogExtensions.ReadParameters(w.Parameters)["new"] == "11" && EventLogExtensions.ReadParameters(w.Parameters)["takenOver"] == "false");
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_EndedSmallerThanLastValue_IsTakenOver(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, 10000);
            await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(5), 12000);
            await TransactionEventAsync(cp, "Ended", uid, 2, start.AddMinutes(20), 11500);

            Assert.Equal(11.5, LoadTransaction(uid).MeterStop!.Value, 6);
            Assert.Single(MeterWarnings(cp.ChargePointId));
        }

        [Theory]
        [MemberData(nameof(Ocpp2xProtocols))]
        public async Task Ocpp2x_IncreasingMeter_NoWarning(string protocol)
        {
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);

            await TransactionEventAsync(cp, "Started", uid, 0, start, 10000);
            await TransactionEventAsync(cp, "Updated", uid, 1, start.AddMinutes(5), 10000);
            await TransactionEventAsync(cp, "Updated", uid, 2, start.AddMinutes(10), 12000);
            await TransactionEventAsync(cp, "Ended", uid, 3, start.AddMinutes(20), 12000);

            Assert.Equal(12.0, LoadTransaction(uid).MeterStop!.Value, 6);
            Assert.Empty(MeterWarnings(cp.ChargePointId));
        }

        [Fact]
        public async Task Ocpp16_StopSmallerThanStart_IsTakenOver()
        {
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            string tagId = _factory.AddChargeTag();

            int transactionId = await StartTransaction16Async(cp, tagId, 5000);
            await StopTransaction16Async(cp, tagId, transactionId, 4000);

            using OCPPCoreContext db = _factory.CreateDbContext();
            Transaction transaction = db.Transactions.Single(t => t.TransactionId == transactionId);
            Assert.Equal(5.0, transaction.MeterStart, 6);
            Assert.Equal(4.0, transaction.MeterStop!.Value, 6);
            ChargePointEvent warning = Assert.Single(MeterWarnings(cp.ChargePointId));
            Assert.StartsWith("StopTransaction:", warning.Details);
            Assert.Equal(EventSeverity.Warning, warning.Severity);
        }

        [Fact]
        public async Task Ocpp16_MeterStartsAtZeroForEachTransaction_NoWarning()
        {
            // some charge points start each transaction at 0 => only values within a transaction are compared
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            string tagId = _factory.AddChargeTag();

            int first = await StartTransaction16Async(cp, tagId, 0);
            await StopTransaction16Async(cp, tagId, first, 3000);
            int second = await StartTransaction16Async(cp, tagId, 0);
            await StopTransaction16Async(cp, tagId, second, 2000);

            Assert.Empty(MeterWarnings(cp.ChargePointId));
        }

        [Fact]
        public async Task Ocpp16_SupersededEstimate_NeverBelowStart()
        {
            // the stop message of the first transaction got lost, the next one starts at 0 (meter per transaction)
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            string tagId = _factory.AddChargeTag();

            int first = await StartTransaction16Async(cp, tagId, 5000);
            await StartTransaction16Async(cp, tagId, 0);

            using (OCPPCoreContext db = _factory.CreateDbContext())
            {
                Transaction transaction = db.Transactions.Single(t => t.TransactionId == first);
                Assert.Equal(TransactionStatus.StopReasonSuperseded, transaction.StopReason);
                // estimate => 0 kWh instead of negative energy, no warning (no value of the charge point)
                Assert.Equal(5.0, transaction.MeterStop!.Value, 6);
            }
            Assert.Empty(MeterWarnings(cp.ChargePointId));
        }

        private async Task<int> StartTransaction16Async(ChargePointSimulator cp, string tagId, int meterStartWh)
        {
            OcppResult result = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = meterStartWh, timestamp = DateTimeOffset.UtcNow });
            return result.Payload!["transactionId"]!.Value<int>();
        }

        private static async Task StopTransaction16Async(ChargePointSimulator cp, string tagId, int transactionId, int meterStopWh)
        {
            OcppResult result = await cp.CallAsync("StopTransaction", new { transactionId, idTag = tagId, meterStop = meterStopWh, timestamp = DateTimeOffset.UtcNow, reason = "Local" });
            Assert.Equal(3, result.MessageType);
        }

        private static Task<OcppResult> TransactionEventAsync(ChargePointSimulator cp, string eventType, string uid, int seqNo, DateTimeOffset timestamp, double meterWh)
        {
            return cp.CallAsync("TransactionEvent", new
            {
                eventType,
                timestamp,
                triggerReason = eventType == "Started" ? "CablePluggedIn" : "MeterValuePeriodic",
                seqNo,
                transactionInfo = new { transactionId = uid },
                evse = new { id = 1, connectorId = 1 },
                meterValue = new object[]
                {
                    new { timestamp, sampledValue = new[] { new { value = meterWh, measurand = "Energy.Active.Import.Register", unitOfMeasure = new { unit = "Wh" } } } }
                }
            });
        }

        private Transaction LoadTransaction(string uid)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.Transactions.Single(t => t.Uid == uid);
        }

        private List<ChargePointEvent> MeterWarnings(string chargePointId)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.ChargePointEvents.Where(e => e.ChargePointId == chargePointId && e.EventCode == EventCode.MeterDecreased).ToList();
        }
    }
}
