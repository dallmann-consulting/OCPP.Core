using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// OCPP 1.6: a repeated StartTransaction (e.g. the response got lost) must not create a second transaction.
    /// Detection: same charge point, connector, start time, meter and tag (OCPP 1.6 has no transaction id of the charge point).
    /// </summary>
    public class RepeatedStartTransactionTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public RepeatedStartTransactionTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        private List<Transaction> LoadTransactions(string chargePointId)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.Transactions.Where(t => t.ChargePointId == chargePointId).OrderBy(t => t.TransactionId).ToList();
        }

        [Fact]
        public async Task RepeatedStart_ReturnsSameTransactionId()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            object start = new { connectorId = 1, idTag = tagId, meterStart = 1234, timestamp = DateTimeOffset.UtcNow.AddMinutes(-5) };

            OcppResult first = await cp.CallAsync("StartTransaction", start);
            OcppResult repeated = await cp.CallAsync("StartTransaction", start);

            Assert.False(repeated.IsError);
            Assert.Equal("Accepted", repeated.Payload!["idTagInfo"]!["status"]!.Value<string>());
            Assert.Equal(first.Payload!["transactionId"]!.Value<int>(), repeated.Payload["transactionId"]!.Value<int>());
            Transaction transaction = Assert.Single(LoadTransactions(cp.ChargePointId));
            // the repeated message must not close the transaction as "superseded"
            Assert.Null(transaction.StopTime);
        }

        [Fact]
        public async Task RepeatedStart_ThenStop_ClosesTransaction()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset startTime = DateTimeOffset.UtcNow.AddMinutes(-30);
            object start = new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = startTime };

            await cp.CallAsync("StartTransaction", start);
            OcppResult repeated = await cp.CallAsync("StartTransaction", start);
            int transactionId = repeated.Payload!["transactionId"]!.Value<int>();
            OcppResult stop = await cp.CallAsync("StopTransaction", new { transactionId, idTag = tagId, meterStop = 6000, timestamp = startTime.AddMinutes(30), reason = "Local" });

            Assert.False(stop.IsError);
            Transaction transaction = Assert.Single(LoadTransactions(cp.ChargePointId));
            Assert.NotNull(transaction.StopTime);
            Assert.Equal(6.0, transaction.MeterStop);
            Assert.Equal("Local", transaction.StopReason);
        }

        [Fact]
        public async Task TwoRealStarts_ShortlyAfterEachOther_AreSeparateTransactions()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset startTime = DateTimeOffset.UtcNow.AddMinutes(-5);

            OcppResult first = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = startTime });
            // second start one second later (e.g. stop of the first transaction lost)
            OcppResult second = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = startTime.AddSeconds(1) });

            Assert.NotEqual(first.Payload!["transactionId"]!.Value<int>(), second.Payload!["transactionId"]!.Value<int>());
            List<Transaction> transactions = LoadTransactions(cp.ChargePointId);
            Assert.Equal(2, transactions.Count);
            Assert.Equal(TransactionStatus.StopReasonSuperseded, transactions[0].StopReason);
            Assert.Null(transactions[1].StopTime);
        }

        [Fact]
        public async Task SameStartValues_OnOtherConnector_AreSeparateTransactions()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset startTime = DateTimeOffset.UtcNow.AddMinutes(-5);

            OcppResult first = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = startTime });
            OcppResult second = await cp.CallAsync("StartTransaction", new { connectorId = 2, idTag = tagId, meterStart = 1000, timestamp = startTime });

            Assert.NotEqual(first.Payload!["transactionId"]!.Value<int>(), second.Payload!["transactionId"]!.Value<int>());
            Assert.Equal(2, LoadTransactions(cp.ChargePointId).Count);
        }

        [Fact]
        public async Task SameStartValues_WithOtherTag_AreSeparateTransactions()
        {
            string tagId = _factory.AddChargeTag();
            string otherTagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset startTime = DateTimeOffset.UtcNow.AddMinutes(-5);

            await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = startTime });
            await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = otherTagId, meterStart = 1000, timestamp = startTime });

            Assert.Equal(2, LoadTransactions(cp.ChargePointId).Count);
        }
    }

    /// <summary>
    /// Server with "DenyConcurrentTx"
    /// </summary>
    public class DenyConcurrentTxServerFactory : OcppServerFactory
    {
        protected override void ConfigureSettings(Dictionary<string, string?> settings)
        {
            settings["DenyConcurrentTx"] = "true";
        }
    }

    public class RepeatedStartTransactionDenyConcurrentTxTests : IClassFixture<DenyConcurrentTxServerFactory>
    {
        private readonly DenyConcurrentTxServerFactory _factory;

        public RepeatedStartTransactionDenyConcurrentTxTests(DenyConcurrentTxServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task RepeatedStart_IsNotRejectedAsConcurrentTransaction()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            object start = new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = DateTimeOffset.UtcNow.AddMinutes(-5) };

            await cp.CallAsync("StartTransaction", start);
            OcppResult repeated = await cp.CallAsync("StartTransaction", start);

            // the open transaction is the same one => not "ConcurrentTx"
            Assert.Equal("Accepted", repeated.Payload!["idTagInfo"]!["status"]!.Value<string>());
        }

        [Fact]
        public async Task SecondRealStart_WithSameTag_IsConcurrentTransaction()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset startTime = DateTimeOffset.UtcNow.AddMinutes(-5);

            await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = startTime });
            OcppResult second = await cp.CallAsync("StartTransaction", new { connectorId = 2, idTag = tagId, meterStart = 500, timestamp = startTime.AddMinutes(1) });

            // the check for concurrent transactions still works for real new transactions
            Assert.Equal("ConcurrentTx", second.Payload!["idTagInfo"]!["status"]!.Value<string>());
        }
    }
}
