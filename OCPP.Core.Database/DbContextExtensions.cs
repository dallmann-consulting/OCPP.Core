using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
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
    }
}
