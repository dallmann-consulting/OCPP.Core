using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;

namespace OCPP.Core.Server.Tests
{
    /// <summary>
    /// The SQL Server scripts must contain all EF migrations (for databases without automatic migration).
    /// Fails after adding a migration => regenerate the generated part of "Update_OCPP.Core.sql"
    /// and add the migration to "Create_OCPP.Core.sql" (see the header of "Update_OCPP.Core.sql").
    /// </summary>
    public class SqlScriptTests
    {
        private static readonly string ScriptDir = Path.Combine(FindRepositoryRoot(), "SQL-Server");

        public static TheoryData<string> Migrations()
        {
            TheoryData<string> data = new TheoryData<string>();
            foreach (string migration in SqlServerMigrations())
            {
                data.Add(migration);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(Migrations))]
        public void UpdateScript_ContainsMigration(string migration)
        {
            string script = File.ReadAllText(Path.Combine(ScriptDir, "Update_OCPP.Core.sql"));

            // the generated part inserts each migration into the history
            Assert.Contains($"VALUES (N'{migration}'", script);
        }

        [Theory]
        [MemberData(nameof(Migrations))]
        public void CreateScript_MarksMigrationAsApplied(string migration)
        {
            string script = File.ReadAllText(Path.Combine(ScriptDir, "Create_OCPP.Core.sql"));

            Assert.Contains($"values ('{migration}'", script);
        }

        [Fact]
        public void Scripts_ContainNoUnknownMigrations()
        {
            // e.g. a removed or renamed migration
            HashSet<string> migrations = SqlServerMigrations().ToHashSet();
            foreach (string scriptName in new[] { "Update_OCPP.Core.sql", "Create_OCPP.Core.sql" })
            {
                string script = File.ReadAllText(Path.Combine(ScriptDir, scriptName));
                foreach (Match match in Regex.Matches(script, @"'(\d{14}_\w+)'"))
                {
                    Assert.True(migrations.Contains(match.Groups[1].Value), $"{scriptName}: unknown migration {match.Groups[1].Value}");
                }
            }
        }

        private static IEnumerable<string> SqlServerMigrations()
        {
            // no database connection needed: the migrations are read from the assembly
            DbContextOptions<OCPPCoreContextSqlServer> options = new DbContextOptionsBuilder<OCPPCoreContextSqlServer>()
                .UseSqlServer("Server=.;Database=OCPP.Core.Unused;")
                .Options;
            using OCPPCoreContextSqlServer dbContext = new OCPPCoreContextSqlServer(options);
            return dbContext.Database.GetMigrations().ToList();
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OCPP.Core.sln")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root (OCPP.Core.sln) not found");
        }
    }
}
