using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChaleAltoDoMorroAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class FolgaEntreHospedes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "buffer_after_nights",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "buffer_before_nights",
                table: "reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    buffer_before_nights = table.Column<int>(type: "integer", nullable: false),
                    buffer_after_nights = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_settings", x => x.id);
                    table.CheckConstraint("ck_booking_settings_buffer_after_range", "buffer_after_nights BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_booking_settings_buffer_before_range", "buffer_before_nights BETWEEN 0 AND 3");
                    table.CheckConstraint("ck_booking_settings_single_row", "id = 1");
                });

            migrationBuilder.InsertData(
                table: "booking_settings",
                columns: new[] { "id", "buffer_after_nights", "buffer_before_nights" },
                values: new object[] { 1, 0, 0 });

            migrationBuilder.AddCheckConstraint(
                name: "ck_reservations_buffer_after_range",
                table: "reservations",
                sql: "buffer_after_nights IS NULL OR buffer_after_nights BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reservations_buffer_before_range",
                table: "reservations",
                sql: "buffer_before_nights IS NULL OR buffer_before_nights BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reservations_buffer_after_range",
                table: "reservations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reservations_buffer_before_range",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "buffer_after_nights",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "buffer_before_nights",
                table: "reservations");
        }
    }
}
