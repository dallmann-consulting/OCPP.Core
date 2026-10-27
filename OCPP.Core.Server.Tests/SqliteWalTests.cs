using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// SQLite in WAL mode: server and management write into the same file at the same time
    /// (in the default mode one of them sometimes gets "database is locked" immediately)
    /// </summary>
    public class SqliteWalTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public SqliteWalTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void ServerStart_SwitchesToWal()
        {
            using OCPPCoreContext db = _factory.CreateDbContext();
            Assert.Equal("wal", JournalMode(db));
        }

        [Fact]
        public async Task ConcurrentWriters_NoLockErrors()
        {
            string file = Path.Combine(Path.GetTempPath(), $"ocpp-core-wal-{Guid.NewGuid():N}.sqlite");
            string connectionString = $"Filename={file};";
            try
            {
                using (OCPPCoreContext db = CreateContext(connectionString))
                {
                    await db.EnsureMigratedAsync();
                    db.EnableSqliteWal(NullLogger.Instance);
                    Assert.Equal("wal", JournalMode(db));
                    db.ChargePoints.Add(new ChargePoint { ChargePointId = "CP1" });
                    db.ChargeTags.Add(new ChargeTag { TagId = "T1", TagName = "Tag" });
                    db.SaveChanges();
                }

                List<string> errors = new List<string>();
                DateTime end = DateTime.UtcNow.AddSeconds(2);
                // like the server: events (direct INSERT) and transactions (SaveChanges)
                Task server = Task.Run(() =>
                {
                    using OCPPCoreContext db = CreateContext(connectionString);
                    while (DateTime.UtcNow < end)
                    {
                        try
                        {
                            db.WriteEvent(null, "CP1", null, EventCategory.Status, EventSeverity.Info, EventCode.StatusChanged);
                            db.Transactions.Add(new Transaction { ChargePointId = "CP1", ConnectorId = 1, StartTime = DateTime.UtcNow });
                            db.SaveChanges();
                        }
                        catch (Exception exp)
                        {
                            lock (errors) errors.Add("server: " + exp.GetBaseException().Message);
                        }
                        db.ChangeTracker.Clear();
                    }
                }, TestContext.Current.CancellationToken);
                // like the management: read and change a tag
                Task management = Task.Run(() =>
                {
                    using OCPPCoreContext db = CreateContext(connectionString);
                    while (DateTime.UtcNow < end)
                    {
                        try
                        {
                            ChargeTag tag = db.ChargeTags.Single(t => t.TagId == "T1");
                            tag.TagName = Guid.NewGuid().ToString();
                            db.SaveChanges();
                        }
                        catch (Exception exp)
                        {
                            lock (errors) errors.Add("management: " + exp.GetBaseException().Message);
                        }
                        db.ChangeTracker.Clear();
                    }
                }, TestContext.Current.CancellationToken);
                await Task.WhenAll(server, management);

                Assert.Empty(errors);
            }
            finally
            {
                SqliteConnection.ClearPool(new SqliteConnection(connectionString));
                foreach (string f in new[] { file, file + "-wal", file + "-shm" })
                {
                    File.Delete(f);
                }
            }
        }

        private static string? JournalMode(OCPPCoreContext db)
        {
            db.Database.OpenConnection();
            try
            {
                using DbCommand cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "PRAGMA journal_mode;";
                return cmd.ExecuteScalar()?.ToString();
            }
            finally
            {
                db.Database.CloseConnection();
            }
        }

        private static OCPPCoreContext CreateContext(string connectionString)
        {
            return new OCPPCoreContextSqlite(new DbContextOptionsBuilder<OCPPCoreContextSqlite>().UseSqlite(connectionString).Options);
        }
    }
}
