using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations.SQLite
{
    public partial class TransactionsIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Transactions_ChargePointId_ConnectorId"" ON ""Transactions"" (""ChargePointId"", ""ConnectorId"")");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_ChargePointId_ConnectorId",
                table: "Transactions");
        }
    }
}
