using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChaleAltoDoMorroAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class MesesAbertos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "booking_open_until",
                table: "booking_settings",
                type: "date",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "booking_settings",
                keyColumn: "id",
                keyValue: 1,
                column: "booking_open_until",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "booking_open_until",
                table: "booking_settings");
        }
    }
}
