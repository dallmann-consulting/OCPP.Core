using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
                services.AddDbContext<OCPPCoreContext>(options => options
                    .UseSqlServer(sqlServerConnectionString)
                    .ConfigureWarnings(IgnorePendingModelChanges), ServiceLifetime.Transient);
            }
            else if (!string.IsNullOrWhiteSpace(sqliteConnectionString))
            {
                services.AddDbContext<OCPPCoreContext>(options => options
                    .UseSqlite(sqliteConnectionString)
                    .ConfigureWarnings(IgnorePendingModelChanges), ServiceLifetime.Transient);
            }
        }

        // Since EF Core 9, Migrate() throws when the model differs from the migration snapshot.
        // The snapshot is SQL Server-specific and lacks the ConnectorStatus -> ChargePoint FK
        // (upstream drift), so this always triggers. EF Core 8 only logged it.
        private static void IgnorePendingModelChanges(WarningsConfigurationBuilder warnings) =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning);
    }
}
