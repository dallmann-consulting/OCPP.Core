using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class ConnectorStatusForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_ConnectorStatus_ChargePoint_ChargePointId",
                table: "ConnectorStatus",
                column: "ChargePointId",
                principalTable: "ChargePoint",
                principalColumn: "ChargePointId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConnectorStatus_ChargePoint_ChargePointId",
                table: "ConnectorStatus");
        }
    }
}
