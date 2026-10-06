using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChaleAltoDoMorroAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrigemDaPreReserva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attribution_source",
                table: "reservations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "first_visit_at",
                table: "reservations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_campaign",
                table: "reservations",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_content",
                table: "reservations",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_medium",
                table: "reservations",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "utm_source",
                table: "reservations",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attribution_source",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "first_visit_at",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "utm_campaign",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "utm_content",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "utm_medium",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "utm_source",
                table: "reservations");
        }
    }
}
