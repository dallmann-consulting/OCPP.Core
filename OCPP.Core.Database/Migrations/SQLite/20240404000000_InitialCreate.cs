using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations.SQLite
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF NOT EXISTS so existing databases are handled safely
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""ChargePoint"" (
                ""ChargePointId"" TEXT NOT NULL CONSTRAINT ""PK_ChargePoint"" PRIMARY KEY,
                ""Name"" TEXT NULL,
                ""Comment"" TEXT NULL,
                ""Username"" TEXT NULL,
                ""Password"" TEXT NULL,
                ""ClientCertThumb"" TEXT NULL
            )");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""ChargeTags"" (
                ""TagId"" TEXT NOT NULL CONSTRAINT ""PK_ChargeKeys"" PRIMARY KEY,
                ""TagName"" TEXT NULL,
                ""ParentTagId"" TEXT NULL,
                ""ExpiryDate"" TEXT NULL,
                ""Blocked"" INTEGER NULL
            )");

            // FK constraint included from the start; SQLite does not support ALTER TABLE ADD CONSTRAINT
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""ConnectorStatus"" (
                ""ChargePointId"" TEXT NOT NULL,
                ""ConnectorId"" INTEGER NOT NULL,
                ""ConnectorName"" TEXT NULL,
                ""LastStatus"" TEXT NULL,
                ""LastStatusTime"" TEXT NULL,
                ""LastMeter"" REAL NULL,
                ""LastMeterTime"" TEXT NULL,
                CONSTRAINT ""PK_ConnectorStatus"" PRIMARY KEY (""ChargePointId"", ""ConnectorId""),
                CONSTRAINT ""FK_ConnectorStatus_ChargePoint_ChargePointId"" FOREIGN KEY (""ChargePointId"") REFERENCES ""ChargePoint"" (""ChargePointId"") ON DELETE CASCADE
            )");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""MessageLog"" (
                ""LogId"" INTEGER NOT NULL CONSTRAINT ""PK_MessageLog"" PRIMARY KEY AUTOINCREMENT,
                ""ChargePointId"" TEXT NOT NULL,
                ""ConnectorId"" INTEGER NULL,
                ""LogTime"" TEXT NOT NULL,
                ""Message"" TEXT NOT NULL,
                ""Result"" TEXT NULL,
                ""ErrorCode"" TEXT NULL
            )");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS ""Transactions"" (
                ""TransactionId"" INTEGER NOT NULL CONSTRAINT ""PK_Transactions"" PRIMARY KEY AUTOINCREMENT,
                ""ChargePointId"" TEXT NOT NULL,
                ""ConnectorId"" INTEGER NOT NULL,
                ""StartTime"" TEXT NOT NULL,
                ""MeterStart"" REAL NOT NULL,
                ""StartTagId"" TEXT NULL,
                ""StartResult"" TEXT NULL,
                ""StopTime"" TEXT NULL,
                ""MeterStop"" REAL NULL,
                ""StopTagId"" TEXT NULL,
                ""StopReason"" TEXT NULL,
                ""Uid"" TEXT NULL,
                CONSTRAINT ""FK_Transactions_ChargePoint"" FOREIGN KEY (""ChargePointId"") REFERENCES ""ChargePoint"" (""ChargePointId"")
            )");

            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""ChargePoint_Identifier"" ON ""ChargePoint"" (""ChargePointId"")");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_MessageLog_ChargePointId"" ON ""MessageLog"" (""LogTime"")");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Transactions");
            migrationBuilder.DropTable("ConnectorStatus");
            migrationBuilder.DropTable("MessageLog");
            migrationBuilder.DropTable("ChargeTags");
            migrationBuilder.DropTable("ChargePoint");
        }
    }
}
