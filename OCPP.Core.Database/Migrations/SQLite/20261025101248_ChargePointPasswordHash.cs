using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OCPP.Core.Database.Migrations.SQLite
{
    /// <inheritdoc />
    public partial class ChargePointPasswordHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing to do: SQLite has no length limit for TEXT columns.
            // The migration exists anyway => same migration state as SQL Server (charge point passwords are hashed after this migration)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
