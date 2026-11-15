using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// A registered charge point must not create any number of connectors (database rows) with arbitrary connector/EVSE IDs:
    /// IDs above the max. connector ID of the charge point create no connector status.
    /// </summary>
    public class MaxConnectorIdTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public MaxConnectorIdTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [MemberData(nameof(OcppProtocols.All), MemberType = typeof(OcppProtocols))]
        public async Task StatusNotification_AboveMax_IsRejected(string protocol)
        {
            string chargePointId = _factory.AddChargePoint(maxConnectorId: 2);
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, protocol);

            OcppResult rejected = await cp.CallAsync("StatusNotification", StatusNotification(protocol, 3));
            OcppResult accepted = await cp.CallAsync("StatusNotification", StatusNotification(protocol, 2));

            Assert.True(rejected.IsError);
            Assert.Equal("PropertyConstraintViolation", rejected.ErrorCode);
            Assert.False(accepted.IsError);
            Assert.Equal(new[] { 2 }, ConnectorIds(chargePointId));
            JObject? connectors = GetOnlineConnectors(chargePointId);
            Assert.NotNull(connectors?["2"]);
            Assert.Null(connectors!["3"]);
        }

        [Fact]
        public async Task AboveMax_OneEventPerConnectorId()
        {
            string chargePointId = _factory.AddChargePoint(maxConnectorId: 2);
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);

            await cp.CallAsync("StatusNotification", StatusNotification(OcppProtocols.Ocpp16, 3));
            await cp.CallAsync("StatusNotification", StatusNotification(OcppProtocols.Ocpp16, 3));
            await cp.CallAsync("StatusNotification", StatusNotification(OcppProtocols.Ocpp16, 4));
            await cp.CallAsync("StatusNotification", StatusNotification(OcppProtocols.Ocpp16, 3));

            List<ChargePointEvent> events = Events(chargePointId, EventCode.ConnectorIdNotAllowed);
            Assert.Equal(new int?[] { 3, 4 }, events.Select(e => e.ConnectorId));
            Assert.All(events, e =>
            {
                Assert.Equal(EventSeverity.Warning, e.Severity);
                Assert.Equal("2", EventLogExtensions.ReadParameters(e.Parameters)["max"]);
            });
        }

        [Fact]
        public async Task AboveMax_NumberOfEventsIsLimited()
        {
            string chargePointId = _factory.AddChargePoint(maxConnectorId: 1);
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);

            for (int evseId = 100; evseId < 130; evseId++)
            {
                await cp.CallAsync("StatusNotification", StatusNotification(OcppProtocols.Ocpp201, evseId));
            }

            Assert.Equal(10, Events(chargePointId, EventCode.ConnectorIdNotAllowed).Count);
            Assert.Empty(ConnectorIds(chargePointId));
        }

        [Fact]
        public async Task MaxRaisedWhileConnected_TakesEffectImmediately()
        {
            string chargePointId = _factory.AddChargePoint(maxConnectorId: 1);
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);

            Assert.True((await cp.CallAsync("StatusNotification", StatusNotification(OcppProtocols.Ocpp201, 2))).IsError);

            // changed in the management
            using (OCPPCoreContext db = _factory.CreateDbContext())
            {
                db.ChargePoints.Where(c => c.ChargePointId == chargePointId).ExecuteUpdate(s => s.SetProperty(c => c.MaxConnectorId, 2));
            }

            Assert.False((await cp.CallAsync("StatusNotification", StatusNotification(OcppProtocols.Ocpp201, 2))).IsError);
            Assert.Equal(new[] { 2 }, ConnectorIds(chargePointId));
        }

        [Fact]
        public async Task Ocpp16_TransactionAboveMax_IsAcceptedWithoutConnectorStatus()
        {
            string chargePointId = _factory.AddChargePoint(maxConnectorId: 2);
            string tagId = _factory.AddChargeTag();
            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp16);

            OcppResult start = await cp.CallAsync("StartTransaction", new { connectorId = 3, idTag = tagId, meterStart = 1000, timestamp = DateTimeOffset.UtcNow.AddMinutes(-5) });

            // the transaction (billing) must not get lost
            Assert.False(start.IsError);
            Assert.Equal("Accepted", start.Payload!["idTagInfo"]!["status"]!.Value<string>());
            using OCPPCoreContext db = _factory.CreateDbContext();
            Transaction transaction = Assert.Single(db.Transactions.Where(t => t.ChargePointId == chargePointId).ToList());
            Assert.Equal(3, transaction.ConnectorId);
            Assert.Empty(ConnectorIds(chargePointId));
            Assert.Null(GetOnlineConnectors(chargePointId)?["3"]);
        }

        [Fact]
        public async Task Connect_DbConnectorsAboveMax_AreNotLoaded()
        {
            // e.g. old entries after the maximum was lowered
            string chargePointId = _factory.AddChargePoint(maxConnectorId: 2);
            using (OCPPCoreContext db = _factory.CreateDbContext())
            {
                db.ConnectorStatuses.Add(new ConnectorStatus() { ChargePointId = chargePointId, ConnectorId = 1, LastStatus = "Available", LastStatusTime = DateTime.UtcNow });
                db.ConnectorStatuses.Add(new ConnectorStatus() { ChargePointId = chargePointId, ConnectorId = 5, LastStatus = "Available", LastStatusTime = DateTime.UtcNow });
                db.SaveChanges();
            }

            await using ChargePointSimulator cp = await _factory.ConnectAsync(chargePointId, OcppProtocols.Ocpp201);

            JObject? connectors = null;
            Assert.True(await Eventually.TrueAsync(() =>
            {
                connectors = GetOnlineConnectors(chargePointId);
                return connectors?["1"] != null;
            }));
            Assert.Null(connectors!["5"]);
        }

        private static object StatusNotification(string protocol, int connectorId)
        {
            if (protocol == OcppProtocols.Ocpp16)
            {
                return new { connectorId, errorCode = "NoError", status = "Available", timestamp = DateTimeOffset.UtcNow };
            }
            return new { timestamp = DateTimeOffset.UtcNow, connectorStatus = "Available", evseId = connectorId, connectorId = 1 };
        }

        private List<int> ConnectorIds(string chargePointId)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.ConnectorStatuses.Where(cs => cs.ChargePointId == chargePointId).OrderBy(cs => cs.ConnectorId).Select(cs => cs.ConnectorId).ToList();
        }

        private List<ChargePointEvent> Events(string chargePointId, string eventCode)
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            return db.ChargePointEvents.AsNoTracking()
                .Where(e => e.ChargePointId == chargePointId && e.EventCode == eventCode)
                .OrderBy(e => e.EventId)
                .ToList();
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
    /// The migration sets the max. connector ID of existing charge points to the highest connector ID used so far
    /// </summary>
    public class MaxConnectorIdMigrationTests
    {
        [Fact]
        public async Task Migration_TakesHighestUsedConnectorId()
        {
            string file = Path.Combine(Path.GetTempPath(), $"ocpp-core-maxconnector-{Guid.NewGuid():N}.sqlite");
            string connectionString = $"Filename={file};";
            try
            {
                // database of an old version (created by the old script, before the migration)
                using (SqliteConnection connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    using SqliteCommand cmd = connection.CreateCommand();
                    cmd.CommandText = File.ReadAllText(RepositoryPaths.File("SQLite", "OCPP.Core.sqlite.sql")) + @"
                        INSERT INTO ChargePoint (ChargePointId) VALUES ('CP-STATUS'), ('CP-TX'), ('CP-NONE');
                        INSERT INTO ConnectorStatus (ChargePointId, ConnectorId) VALUES ('CP-STATUS', 1), ('CP-STATUS', 4), ('CP-TX', 2);
                        INSERT INTO Transactions (ChargePointId, ConnectorId, StartTime, MeterStart) VALUES ('CP-STATUS', 3, '2026-01-01', 0), ('CP-TX', 6, '2026-01-01', 0);";
                    cmd.ExecuteNonQuery();
                }

                using (OCPPCoreContext dbContext = CreateContext(connectionString))
                {
                    await dbContext.EnsureMigratedAsync();
                }

                using OCPPCoreContext check = CreateContext(connectionString);
                Dictionary<string, int> maxIds = check.ChargePoints.ToDictionary(c => c.ChargePointId, c => c.MaxConnectorId);
                Assert.Equal(4, maxIds["CP-STATUS"]);
                Assert.Equal(6, maxIds["CP-TX"]);
                Assert.Equal(1, maxIds["CP-NONE"]);
            }
            finally
            {
                // only the pool of this database (ClearAllPools() would affect other test classes)
                SqliteConnection.ClearPool(new SqliteConnection(connectionString));
                File.Delete(file);
            }
        }

        private static OCPPCoreContext CreateContext(string connectionString)
        {
            DbContextOptions<OCPPCoreContextSqlite> options = new DbContextOptionsBuilder<OCPPCoreContextSqlite>()
                .UseSqlite(connectionString)
                .Options;
            return new OCPPCoreContextSqlite(options);
        }
    }
}
