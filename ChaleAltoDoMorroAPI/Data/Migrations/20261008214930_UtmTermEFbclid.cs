using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChaleAltoDoMorroAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class UtmTermEFbclid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fbclid",
                table: "reservations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_term",
                table: "reservations",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fbclid",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "utm_term",
                table: "reservations");
        }
    }
}
