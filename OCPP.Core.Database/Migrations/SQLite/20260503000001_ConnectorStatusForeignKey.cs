using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations.SQLite
{
    // SQLite: FK was already defined in InitialCreate (SQLite does not support ALTER TABLE ADD CONSTRAINT)
    public partial class ConnectorStatusForeignKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
