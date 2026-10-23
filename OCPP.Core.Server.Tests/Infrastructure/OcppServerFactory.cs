using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OCPP.Core.Database;

namespace OCPP.Core.Server.Tests.Infrastructure
{
    /// <summary>
    /// Starts the OCPP server in memory (TestServer) with its own SQLite database file.
    /// One instance per test class (IClassFixture) => test classes are isolated from each other.
    /// Override <see cref="ConfigureSettings"/> for different server settings.
    /// </summary>
    public class OcppServerFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"ocpp-core-test-{Guid.NewGuid():N}.sqlite");

        /// <summary>
        /// Connection string of the test database
        /// </summary>
        public string ConnectionString => $"Filename={_dbFile};";

        /// <summary>
        /// Server settings (appsettings) for the test run. Derived factories can change them.
        /// </summary>
        protected virtual void ConfigureSettings(Dictionary<string, string?> settings)
        {
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Not "Development" => appsettings.Development.json (SQL Server, dump dir, ...) is not loaded
            builder.UseEnvironment("Testing");

            Dictionary<string, string?> settings = new Dictionary<string, string?>()
            {
                ["ConnectionStrings:SQLite"] = ConnectionString,
                ["ConnectionStrings:SqlServer"] = "",
                ["AutoMigrateDB"] = "true",
                ["MessageDumpDir"] = "",
                ["DbMessageLog"] = "0",
                ["ValidateMessages"] = "true",
                ["ApiKey"] = "",
                ["HeartBeatInterval"] = "300",
                ["Serilog:MinimumLevel:Default"] = "Warning",
            };
            ConfigureSettings(settings);

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(settings);
            });
        }

        /// <summary>
        /// Creates a new DbContext on the test database (for seeding and assertions)
        /// </summary>
        public OCPPCoreContext CreateDbContext()
        {
            EnsureServerStarted();
            DbContextOptions<OCPPCoreContextSqlite> options = new DbContextOptionsBuilder<OCPPCoreContextSqlite>()
                .UseSqlite(ConnectionString)
                .Options;
            return new OCPPCoreContextSqlite(options);
        }

        /// <summary>
        /// Creates a charge point with a unique ID in the database and returns the ID
        /// </summary>
        public string AddChargePoint(string? username = null, string? password = null)
        {
            string chargePointId = "CP-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            using OCPPCoreContext dbContext = CreateDbContext();
            dbContext.ChargePoints.Add(new ChargePoint()
            {
                ChargePointId = chargePointId,
                Name = chargePointId,
                Username = username,
                Password = password
            });
            dbContext.SaveChanges();
            return chargePointId;
        }

        /// <summary>
        /// Creates a charge tag with a unique ID in the database and returns the ID
        /// </summary>
        public string AddChargeTag(bool blocked = false, DateTime? expiryDate = null, string? parentTagId = null)
        {
            // no '_' in the ID => CleanChargeTagId() doesn't change it
            string tagId = "TAG" + Guid.NewGuid().ToString("N").Substring(0, 12);
            using OCPPCoreContext dbContext = CreateDbContext();
            dbContext.ChargeTags.Add(new ChargeTag()
            {
                TagId = tagId,
                TagName = tagId,
                Blocked = blocked,
                ExpiryDate = expiryDate,
                ParentTagId = parentTagId
            });
            dbContext.SaveChanges();
            return tagId;
        }

        /// <summary>
        /// Connects a simulated charge point via WebSocket
        /// </summary>
        public Task<ChargePointSimulator> ConnectAsync(string chargePointId, string protocol, string? username = null, string? password = null)
        {
            EnsureServerStarted();
            return ChargePointSimulator.ConnectAsync(Server, chargePointId, protocol, username, password);
        }

        /// <summary>
        /// Starts the host (=> DB migration in Startup.Configure)
        /// </summary>
        private void EnsureServerStarted()
        {
            _ = Server;
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            SqliteConnection.ClearAllPools();
            try
            {
                File.Delete(_dbFile);
            }
            catch (IOException)
            {
                // file still in use => remains in the temp directory
            }
        }
    }
}
