using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations
{
    /// <inheritdoc />
    public partial class TagLengthMessageLogIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_MessageLog_ChargePointId",
                table: "MessageLog",
                newName: "IX_MessageLog_LogTime");

            migrationBuilder.AlterColumn<string>(
                name: "StopTagId",
                table: "Transactions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StartTagId",
                table: "Transactions",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ParentTagId",
                table: "ChargeTags",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TagId",
                table: "ChargeTags",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_MessageLog_ChargePointId",
                table: "MessageLog",
                columns: new[] { "ChargePointId", "LogTime" });

            // the view shows the tag columns => refresh its metadata (column lengths)
            migrationBuilder.Sql("IF OBJECT_ID(N'[dbo].[ConnectorStatusView]', N'V') IS NOT NULL EXEC sp_refreshview N'[dbo].[ConnectorStatusView]';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageLog_ChargePointId",
                table: "MessageLog");

            migrationBuilder.RenameIndex(
                name: "IX_MessageLog_LogTime",
                table: "MessageLog",
                newName: "IX_MessageLog_ChargePointId");

            migrationBuilder.AlterColumn<string>(
                name: "StopTagId",
                table: "Transactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StartTagId",
                table: "Transactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ParentTagId",
                table: "ChargeTags",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TagId",
                table: "ChargeTags",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.Sql("IF OBJECT_ID(N'[dbo].[ConnectorStatusView]', N'V') IS NOT NULL EXEC sp_refreshview N'[dbo].[ConnectorStatusView]';");
        }
    }
}
