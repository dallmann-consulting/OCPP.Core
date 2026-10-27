using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// OCPP 2.x stop reason = "transactionInfo.stoppedReason" (not the "triggerReason" of the message).
    /// Spec: stoppedReason "MAY only be omitted when Reason is Local".
    /// </summary>
    public class StopReasonTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public StopReasonTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData(OcppProtocols.Ocpp201, "EVDisconnected", "EVDisconnected")]
        [InlineData(OcppProtocols.Ocpp21, "EVDisconnected", "EVDisconnected")]
        [InlineData(OcppProtocols.Ocpp201, "Remote", "Remote")]
        [InlineData(OcppProtocols.Ocpp21, "PowerLoss", "PowerLoss")]
        // without stoppedReason => "Local"
        [InlineData(OcppProtocols.Ocpp201, null, "Local")]
        [InlineData(OcppProtocols.Ocpp21, null, "Local")]
        public async Task Ended_StopReasonIsStoppedReason(string protocol, string? stoppedReason, string expected)
        {
            string chargePointId = _factory.AddChargePoint();
            string uid = Guid.NewGuid().ToString();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-10);

            await TransactionEventAsync(cp, "Started", uid, 0, start, "CablePluggedIn", null);
            OcppResult result = await TransactionEventAsync(cp, "Ended", uid, 1, start.AddMinutes(10), "EVCommunicationLost", stoppedReason);
            Assert.Equal(3, result.MessageType);

            using OCPPCoreContext db = _factory.CreateDbContext();
            Transaction transaction = db.Transactions.AsNoTracking().Single(t => t.Uid == uid);
            // the trigger of the message ("EVCommunicationLost") isn't the reason
            Assert.Equal(expected, transaction.StopReason);

            ChargePointEvent stopped = db.ChargePointEvents.AsNoTracking().Single(e => e.ChargePointId == chargePointId && e.EventCode == EventCode.TransactionStopped);
            Assert.Equal(expected, EventLogExtensions.ReadParameters(stopped.Parameters)["reason"]);
        }

        private static Task<OcppResult> TransactionEventAsync(ChargePointSimulator cp, string eventType, string uid, int seqNo, DateTimeOffset timestamp, string triggerReason, string? stoppedReason)
        {
            Dictionary<string, object> transactionInfo = new Dictionary<string, object> { ["transactionId"] = uid };
            if (stoppedReason != null) transactionInfo["stoppedReason"] = stoppedReason;

            return cp.CallAsync("TransactionEvent", new Dictionary<string, object>
            {
                ["eventType"] = eventType,
                ["timestamp"] = timestamp,
                ["triggerReason"] = triggerReason,
                ["seqNo"] = seqNo,
                ["transactionInfo"] = transactionInfo,
                ["evse"] = new { id = 1, connectorId = 1 },
                ["meterValue"] = new object[]
                {
                    new { timestamp, sampledValue = new[] { new { value = 1000.0 + seqNo * 1000, measurand = "Energy.Active.Import.Register", unitOfMeasure = new { unit = "Wh" } } } }
                }
            });
        }
    }
}
