using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations.SQLite
{
    /// <inheritdoc />
    public partial class TagLengthMessageLogIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tag lengths (OCPP 2.1 idToken up to 255): nothing to do, SQLite has no length limit.
            // MessageLog: the old index "IX_MessageLog_ChargePointId" is on LogTime => new name.
            // Raw SQL with IF (NOT) EXISTS: databases created by the old script "OCPP.Core.sqlite.sql" don't have the old index
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_MessageLog_ChargePointId\";");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_MessageLog_LogTime\" ON \"MessageLog\" (\"LogTime\");");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_MessageLog_ChargePointId\" ON \"MessageLog\" (\"ChargePointId\", \"LogTime\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_MessageLog_ChargePointId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_MessageLog_LogTime\";");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_MessageLog_ChargePointId\" ON \"MessageLog\" (\"LogTime\");");
        }
    }
}
