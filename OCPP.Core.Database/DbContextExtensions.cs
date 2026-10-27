using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OCPP.Core.Database
{
    public static class DbContextExtensions
    {
        public static void AddOCPPDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            string sqlServerConnectionString = configuration.GetConnectionString("SqlServer");
            string sqliteConnectionString = configuration.GetConnectionString("SQLite");

            if (!string.IsNullOrWhiteSpace(sqlServerConnectionString))
            {
                services.AddDbContext<OCPPCoreContext, OCPPCoreContextSqlServer>(
                    options => options.UseSqlServer(sqlServerConnectionString), ServiceLifetime.Transient);
            }
            else if (!string.IsNullOrWhiteSpace(sqliteConnectionString))
            {
                /*
                // => Check for configured db-file and throw error if it does not exist
                var match = Regex.Match(sqliteConnectionString,
                    @"(?:Filename|Data Source)\s*=\s*([^;]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string dbPath = Path.GetFullPath(
                        Environment.ExpandEnvironmentVariables(match.Groups[1].Value.Trim()));
                    if (!File.Exists(dbPath))
                        throw new FileNotFoundException(
                            $"SQLite database file not found: '{dbPath}'. Check the 'ConnectionStrings:SQLite' setting in appsettings.json.",
                            dbPath);
                }
                */
                services.AddDbContext<OCPPCoreContext, OCPPCoreContextSqlite>(
                    options => options.UseSqlite(sqliteConnectionString), ServiceLifetime.Transient);
            }
        }

        /// <summary>
        /// SQLite: switches the database to WAL mode (write-ahead log).
        /// Server and management write into the same file at the same time. In the default mode (rollback journal)
        /// one of them sometimes gets "database is locked" immediately, in WAL mode the writers wait for each other.
        /// The mode is stored in the database file => it's changed only once. No effect for SQL Server.
        /// Note: SQLite then also uses the files "-wal" and "-shm" next to the database (backup: all files or after a checkpoint);
        /// WAL doesn't work on network drives.
        /// </summary>
        public static void EnableSqliteWal(this OCPPCoreContext context, ILogger logger)
        {
            if (context.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true) return;

            try
            {
                context.Database.OpenConnection();
                try
                {
                    using DbCommand cmd = context.Database.GetDbConnection().CreateCommand();
                    cmd.CommandText = "PRAGMA journal_mode;";
                    string mode = cmd.ExecuteScalar()?.ToString();
                    if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
                    {
                        cmd.CommandText = "PRAGMA journal_mode=WAL;";
                        string newMode = cmd.ExecuteScalar()?.ToString();
                        if (string.Equals(newMode, "wal", StringComparison.OrdinalIgnoreCase))
                        {
                            logger?.LogInformation("Database => SQLite journal mode changed from '{0}' to 'wal'", mode);
                        }
                        else
                        {
                            // e.g. network drive or the database is locked by another process (next try at the next start)
                            logger?.LogWarning("Database => SQLite journal mode couldn't be changed to 'wal' (current mode: '{0}'). Concurrent writes may fail with 'database is locked'.", newMode);
                        }
                    }
                }
                finally
                {
                    context.Database.CloseConnection();
                }
            }
            catch (Exception exp)
            {
                logger?.LogError(exp, "Database => Error setting the SQLite journal mode: {0}", exp.Message);
            }
        }
    }
}
