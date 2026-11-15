using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations.SQLite
{
    /// <inheritdoc />
    public partial class ChargePointMaxConnectorId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxConnectorId",
                table: "ChargePoint",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // existing charge points: highest connector ID used so far (connector status or transactions), otherwise 1
            migrationBuilder.Sql(
                "UPDATE ChargePoint SET MaxConnectorId = (SELECT MAX(cs.ConnectorId) FROM ConnectorStatus cs WHERE cs.ChargePointId = ChargePoint.ChargePointId) " +
                "WHERE (SELECT MAX(cs.ConnectorId) FROM ConnectorStatus cs WHERE cs.ChargePointId = ChargePoint.ChargePointId) > MaxConnectorId");
            migrationBuilder.Sql(
                "UPDATE ChargePoint SET MaxConnectorId = (SELECT MAX(t.ConnectorId) FROM Transactions t WHERE t.ChargePointId = ChargePoint.ChargePointId) " +
                "WHERE (SELECT MAX(t.ConnectorId) FROM Transactions t WHERE t.ChargePointId = ChargePoint.ChargePointId) > MaxConnectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxConnectorId",
                table: "ChargePoint");
        }
    }
}
