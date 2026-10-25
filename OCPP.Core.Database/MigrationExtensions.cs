using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OCPP.Core.Database
{
    public static class MigrationExtensions
    {
        /// <summary>
        /// Bootstraps the EF migration history for pre-migration databases (created from SQL scripts),
        /// then applies all pending migrations. Safe to call on fresh and already-migrated databases.
        /// </summary>
        public static async Task EnsureMigratedAsync(this OCPPCoreContext context, ILogger logger = null)
        {
            await BootstrapMigrationsIfNeededAsync(context, logger);
            await context.Database.MigrateAsync();
        }

        /// <summary>
        /// Synchronous wrapper for use in non-async startup paths (e.g. ASP.NET Core Startup.Configure).
        /// </summary>
        public static void EnsureMigrated(this OCPPCoreContext context, ILogger logger = null)
            => EnsureMigratedAsync(context, logger).GetAwaiter().GetResult();

        /// <summary>
        /// Migration that widens the column for charge point passwords => passwords can be stored as hash
        /// </summary>
        public const string ChargePointPasswordHashMigration = "ChargePointPasswordHash";

        /// <summary>
        /// Names of the migrations that are not applied to the database yet
        /// (e.g. "AutoMigrateDB": false or a database login without the rights for schema changes).
        /// Only reads the migration history => no special database rights needed.
        /// </summary>
        public static List<string> GetPendingMigrationNames(this OCPPCoreContext context)
        {
            return context.Database.GetPendingMigrations().ToList();
        }

        /// <summary>
        /// True if the migration with this name (without the timestamp prefix) is applied to the database
        /// </summary>
        public static bool IsMigrationApplied(this OCPPCoreContext context, string migrationName)
        {
            return context.Database.GetAppliedMigrations().Any(m => m.EndsWith("_" + migrationName, StringComparison.Ordinal));
        }

        /// <summary>
        /// Logs a warning if migrations are pending (the database schema is older than the application)
        /// </summary>
        public static void LogPendingMigrations(this OCPPCoreContext context, ILogger logger)
        {
            try
            {
                List<string> pending = context.GetPendingMigrationNames();
                if (pending.Count > 0)
                {
                    logger?.LogWarning("Database => {0} migration(s) pending: {1}. Apply them with 'AutoMigrateDB' or the SQL scripts (see Installation.md).",
                        pending.Count, string.Join(", ", pending));
                }
            }
            catch (Exception exp)
            {
                logger?.LogError(exp, "Database => Error checking the migration state: {0}", exp.Message);
            }
        }

        /// <summary>
        /// Detects databases created from SQL scripts before EF migrations were introduced.
        /// Creates __EFMigrationsHistory and marks the pre-existing OCPP migrations as applied,
        /// so that the next Migrate() call only adds the new Identity tables.
        /// </summary>
        private static async Task BootstrapMigrationsIfNeededAsync(OCPPCoreContext context, ILogger logger)
        {
            bool isSqlite = context.Database.ProviderName?
                .Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

            if (!await TableExistsAsync(context, "ChargePoint", isSqlite))
                return; // Fresh database — nothing to bootstrap

            if (await TableExistsAsync(context, "__EFMigrationsHistory", isSqlite))
                return; // Already using migrations

            logger?.LogInformation("Pre-migration database detected. Bootstrapping migration history...");

            if (isSqlite)
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                        ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
                        ""ProductVersion"" TEXT NOT NULL
                    )");
                await context.Database.ExecuteSqlRawAsync(@"
                    INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") VALUES
                    ('20240404000000_InitialCreate',            '10.0.9'),
                    ('20240405204318_TransactionsIndex',        '10.0.9'),
                    ('20260503000001_ConnectorStatusForeignKey','10.0.9')");
            }
            else
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = '__EFMigrationsHistory')
                    CREATE TABLE [__EFMigrationsHistory] (
                        [MigrationId] nvarchar(150) NOT NULL,
                        [ProductVersion] nvarchar(32) NOT NULL,
                        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                    )");
                await context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20240404000000_InitialCreate')
                        INSERT INTO [__EFMigrationsHistory] VALUES ('20240404000000_InitialCreate','10.0.9')
                    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20240405204318_TransactionsIndex')
                        INSERT INTO [__EFMigrationsHistory] VALUES ('20240405204318_TransactionsIndex','10.0.9')
                    IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260503000001_ConnectorStatusForeignKey')
                        INSERT INTO [__EFMigrationsHistory] VALUES ('20260503000001_ConnectorStatusForeignKey','10.0.9')");
            }

            logger?.LogInformation("Migration history bootstrapped successfully.");
        }

        private static async Task<bool> TableExistsAsync(OCPPCoreContext context, string tableName, bool isSqlite)
        {
            try
            {
                // Let EF Core open the connection so it properly initializes the connection string
                await context.Database.OpenConnectionAsync();
                try
                {
                    DbConnection conn = context.Database.GetDbConnection();
                    await using DbCommand cmd = conn.CreateCommand();
                    cmd.CommandText = isSqlite
                        ? $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}'"
                        : $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='{tableName}'";
                    var result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(result) > 0;
                }
                finally
                {
                    await context.Database.CloseConnectionAsync();
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
