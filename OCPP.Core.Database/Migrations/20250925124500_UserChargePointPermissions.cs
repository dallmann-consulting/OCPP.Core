using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class UserChargePointPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserChargePoints",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ChargePointId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChargePoints", x => new { x.UserId, x.ChargePointId });
                    table.ForeignKey(
                        name: "FK_UserChargePoints_ChargePoint_ChargePointId",
                        column: x => x.ChargePointId,
                        principalTable: "ChargePoint",
                        principalColumn: "ChargePointId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserChargePoints_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserChargePoints_ChargePointId",
                table: "UserChargePoints",
                column: "ChargePointId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserChargePoints");
        }
    }
}
