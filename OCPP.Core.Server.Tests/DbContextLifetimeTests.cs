using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// The DbContext of a WebSocket connection lives as long as the connection.
    /// Changes in the database (e.g. tag blocked in the management UI) must be effective immediately,
    /// not only after a reconnect of the charging station.
    /// </summary>
    public class DbContextLifetimeTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public DbContextLifetimeTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        private void UpdateChargeTag(string tagId, Action<ChargeTag> update)
        {
            // like the management UI: own DbContext
            using OCPPCoreContext db = _factory.CreateDbContext();
            ChargeTag tag = db.ChargeTags.Find(tagId)!;
            update(tag);
            db.SaveChanges();
        }

        private static object AuthorizePayload(string protocol, string tagId) => (protocol == OcppProtocols.Ocpp16)
            ? new { idTag = tagId }
            : new { idToken = new { idToken = tagId, type = "ISO14443" } };

        private static string? AuthorizeStatus(string protocol, OcppResult result) => (protocol == OcppProtocols.Ocpp16)
            ? result.Payload!["idTagInfo"]!["status"]!.Value<string>()
            : result.Payload!["idTokenInfo"]!["status"]!.Value<string>();

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task TagBlockedWhileConnected_IsBlockedImmediately(string protocol)
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            OcppResult first = await cp.CallAsync("Authorize", AuthorizePayload(protocol, tagId));
            Assert.Equal("Accepted", AuthorizeStatus(protocol, first));

            UpdateChargeTag(tagId, tag => tag.Blocked = true);

            OcppResult second = await cp.CallAsync("Authorize", AuthorizePayload(protocol, tagId));
            Assert.Equal("Blocked", AuthorizeStatus(protocol, second));
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task TagUnblockedWhileConnected_IsAcceptedImmediately(string protocol)
        {
            string tagId = _factory.AddChargeTag(blocked: true);
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), protocol);

            OcppResult first = await cp.CallAsync("Authorize", AuthorizePayload(protocol, tagId));
            Assert.Equal("Blocked", AuthorizeStatus(protocol, first));

            UpdateChargeTag(tagId, tag => tag.Blocked = false);

            OcppResult second = await cp.CallAsync("Authorize", AuthorizePayload(protocol, tagId));
            Assert.Equal("Accepted", AuthorizeStatus(protocol, second));
        }

        [Fact]
        public async Task TagExpiredWhileConnected_IsExpiredImmediately()
        {
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);

            await cp.CallAsync("Authorize", new { idTag = tagId });
            UpdateChargeTag(tagId, tag => tag.ExpiryDate = DateTime.Now.AddDays(-1));

            OcppResult second = await cp.CallAsync("Authorize", new { idTag = tagId });
            Assert.Equal("Expired", second.Payload!["idTagInfo"]!["status"]!.Value<string>());
        }

        [Fact]
        public async Task TransactionChangedInDatabase_IsSeenByConnection()
        {
            // e.g. transaction closed manually in the database while the charge point is connected
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(_factory.AddChargePoint(), OcppProtocols.Ocpp16);
            DateTimeOffset start = DateTimeOffset.UtcNow.AddMinutes(-30);
            OcppResult startResult = await cp.CallAsync("StartTransaction", new { connectorId = 1, idTag = tagId, meterStart = 1000, timestamp = start });
            int transactionId = startResult.Payload!["transactionId"]!.Value<int>();

            using (OCPPCoreContext db = _factory.CreateDbContext())
            {
                Transaction transaction = db.Transactions.Find(transactionId)!;
                transaction.StopTime = start.AddMinutes(10).UtcDateTime;
                transaction.MeterStop = 2.0;
                transaction.StopReason = "Manual";
                db.SaveChanges();
            }

            // the stop of the charge point is a repeated stop for the server now => data unchanged
            await cp.CallAsync("StopTransaction", new { transactionId, idTag = tagId, meterStop = 5000, timestamp = start.AddMinutes(30), reason = "Local" });

            using (OCPPCoreContext db = _factory.CreateDbContext())
            {
                Transaction transaction = db.Transactions.Find(transactionId)!;
                Assert.Equal("Manual", transaction.StopReason);
                Assert.Equal(2.0, transaction.MeterStop);
            }
        }
    }
}
