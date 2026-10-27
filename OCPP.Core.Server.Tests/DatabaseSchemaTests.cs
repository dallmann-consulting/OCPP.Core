using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OCPP.Core.Database;
using OCPP.Core.Server.Tests.Infrastructure;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// Tag columns long enough for OCPP 2.1 (idToken up to 255 characters)
    /// </summary>
    public class TagColumnLengthTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public TagColumnLengthTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData(typeof(ChargeTag), nameof(ChargeTag.TagId))]
        [InlineData(typeof(ChargeTag), nameof(ChargeTag.ParentTagId))]
        [InlineData(typeof(Transaction), nameof(Transaction.StartTagId))]
        [InlineData(typeof(Transaction), nameof(Transaction.StopTagId))]
        public void TagColumn_Has255Characters(Type entity, string property)
        {
            // SQLite ignores the length => check the model (it is used for the SQL Server migrations)
            using OCPPCoreContext dbContext = _factory.CreateDbContext();
            IProperty? p = dbContext.Model.FindEntityType(entity)?.FindProperty(property);

            Assert.NotNull(p);
            Assert.Equal(255, p.GetMaxLength());
        }
    }

    /// <summary>
    /// MessageLog indexes and cleanup of old entries
    /// </summary>
    public class MessageLogTests : IClassFixture<OcppServerFactory>
    {
        private readonly OcppServerFactory _factory;

        public MessageLogTests(OcppServerFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("SELECT * FROM \"MessageLog\" WHERE \"ChargePointId\" = 'CP1' ORDER BY \"LogTime\" DESC", "IX_MessageLog_ChargePointId")]
        [InlineData("SELECT \"LogId\" FROM \"MessageLog\" WHERE \"LogTime\" < '2026-01-01'", "IX_MessageLog_LogTime")]
        public void Query_UsesIndex(string sql, string index)
        {
            using OCPPCoreContext dbContext = _factory.CreateDbContext();
            dbContext.Database.OpenConnection();
            using DbCommand cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "EXPLAIN QUERY PLAN " + sql;
            using DbDataReader reader = cmd.ExecuteReader();
            List<string> plan = new List<string>();
            while (reader.Read())
            {
                plan.Add(reader.GetString(3));
            }

            Assert.Contains(plan, p => p.Contains(index));
        }

        [Fact]
        public async Task DeleteOldEntries_DeletesOnlyOldEntriesInBlocks()
        {
            string chargePointId = "LOG-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            DateTime cutoff = DateTime.UtcNow.AddDays(-90);
            using (OCPPCoreContext dbContext = _factory.CreateDbContext())
            {
                for (int i = 0; i < 7; i++)
                {
                    dbContext.MessageLogs.Add(new MessageLog() { ChargePointId = chargePointId, Message = "old", LogTime = cutoff.AddDays(-1 - i) });
                }
                dbContext.MessageLogs.Add(new MessageLog() { ChargePointId = chargePointId, Message = "new", LogTime = cutoff.AddMinutes(1) });
                dbContext.MessageLogs.Add(new MessageLog() { ChargePointId = chargePointId, Message = "new", LogTime = DateTime.UtcNow });
                dbContext.SaveChanges();
            }

            using (OCPPCoreContext dbContext = _factory.CreateDbContext())
            {
                // block size 3 => 3 blocks (3 + 3 + 1)
                int deleted = await LogCleanupService.DeleteOldMessageLogEntriesAsync(dbContext, cutoff, 3, CancellationToken.None);
                Assert.Equal(7, deleted);
            }

            using (OCPPCoreContext dbContext = _factory.CreateDbContext())
            {
                List<MessageLog> remaining = dbContext.MessageLogs.Where(l => l.ChargePointId == chargePointId).ToList();
                Assert.Equal(2, remaining.Count);
                Assert.All(remaining, l => Assert.Equal("new", l.Message));
            }
        }
    }

    /// <summary>
    /// Server and Management migrate the database at the same time (e.g. Docker)
    /// </summary>
    public class ConcurrentMigrationTests
    {
        [Fact]
        public async Task NewDatabase_ParallelMigration_Succeeds()
        {
            await RunParallelMigrationAsync(prepare: null);
        }

        [Fact]
        public async Task PreMigrationDatabase_ParallelBootstrap_Succeeds()
        {
            // database created by the old script (no migration history) => bootstrap + migrations
            string script = File.ReadAllText(RepositoryPaths.File("SQLite", "OCPP.Core.sqlite.sql"));
            for (int i = 0; i < 5; i++)
            {
                await RunParallelMigrationAsync(prepare: connectionString =>
                {
                    using SqliteConnection connection = new SqliteConnection(connectionString);
                    connection.Open();
                    using SqliteCommand cmd = connection.CreateCommand();
                    cmd.CommandText = script;
                    cmd.ExecuteNonQuery();
                });
            }
        }

        [Fact]
        public void SqliteTransaction_TakesWriteLock()
        {
            // the bootstrap relies on it: BeginTransaction() = "BEGIN IMMEDIATE" => a second writer has to wait
            string file = Path.Combine(Path.GetTempPath(), $"ocpp-core-lock-{Guid.NewGuid():N}.sqlite");
            try
            {
                using OCPPCoreContext first = CreateContext($"Filename={file};");
                first.Database.ExecuteSqlRaw("CREATE TABLE Dummy (Id INTEGER)");
                using var transaction = first.Database.BeginTransaction();

                using SqliteConnection second = new SqliteConnection($"Filename={file};Default Timeout=1;");
                second.Open();
                using SqliteCommand cmd = second.CreateCommand();
                cmd.CommandText = "BEGIN IMMEDIATE";
                SqliteException exp = Assert.Throws<SqliteException>(() => cmd.ExecuteNonQuery());
                Assert.Equal(5, exp.SqliteErrorCode);   // SQLITE_BUSY
            }
            finally
            {
                // only the pools of this database (ClearAllPools() would affect other test classes)
                SqliteConnection.ClearPool(new SqliteConnection($"Filename={file};"));
                SqliteConnection.ClearPool(new SqliteConnection($"Filename={file};Default Timeout=1;"));
                File.Delete(file);
            }
        }

        private static async Task RunParallelMigrationAsync(Action<string>? prepare)
        {
            string file = Path.Combine(Path.GetTempPath(), $"ocpp-core-migrate-{Guid.NewGuid():N}.sqlite");
            string connectionString = $"Filename={file};";
            try
            {
                prepare?.Invoke(connectionString);

                // two "processes" (own contexts and connections) start at the same time
                using Barrier barrier = new Barrier(2);
                Task[] tasks = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
                {
                    using OCPPCoreContext dbContext = CreateContext(connectionString);
                    barrier.SignalAndWait();
                    await dbContext.EnsureMigratedAsync();
                })).ToArray();
                await Task.WhenAll(tasks);

                using OCPPCoreContext check = CreateContext(connectionString);
                Assert.Empty(check.GetPendingMigrationNames());
                Assert.Equal(check.Database.GetMigrations().Count(), check.Database.GetAppliedMigrations().Count());
                // schema usable
                Assert.Equal(0, check.ChargePoints.Count());
                Assert.Equal(0, check.MessageLogs.Count());
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
