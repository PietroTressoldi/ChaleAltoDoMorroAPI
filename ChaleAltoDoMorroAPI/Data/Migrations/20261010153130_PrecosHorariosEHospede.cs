using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChaleAltoDoMorroAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class PrecosHorariosEHospede : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "long_stay_price_per_night",
                table: "pricing_rules",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            // Preços do patch 1 (task 23) num banco que JÁ tem preços (produção). Num banco vazio não faz nada:
            // o DevSeeder só semeia quando a tabela está vazia, então inserir aqui faria ele pular o weekday e o weekend.
            migrationBuilder.Sql("""
                UPDATE pricing_rules SET long_stay_price_per_night = 645
                 WHERE kind = 'Weekday' AND long_stay_price_per_night IS NULL;
                UPDATE pricing_rules SET label = 'Fim de semana (sex, sáb e dom)'
                 WHERE kind = 'Weekend';
                INSERT INTO pricing_rules (kind, label, price_per_night, min_nights)
                SELECT 'Extended', 'Diária estendida (sex → sáb ou sáb → dom)', 1190, NULL
                 WHERE EXISTS (SELECT 1 FROM pricing_rules)
                   AND NOT EXISTS (SELECT 1 FROM pricing_rules WHERE kind = 'Extended');
                """);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "guests",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "social_media",
                table: "guests",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "check_in_time",
                table: "booking_settings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "check_out_time",
                table: "booking_settings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "extended_check_in_time",
                table: "booking_settings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "extended_check_out_time",
                table: "booking_settings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.UpdateData(
                table: "booking_settings",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "check_in_time", "check_out_time", "extended_check_in_time", "extended_check_out_time" },
                values: new object[] { new TimeOnly(15, 0, 0), new TimeOnly(12, 0, 0), new TimeOnly(10, 30, 0), new TimeOnly(16, 0, 0) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O código antigo não conhece o kind 'Extended' (quebraria ao ler o enum).
            migrationBuilder.Sql("DELETE FROM pricing_rules WHERE kind = 'Extended';");

            migrationBuilder.DropColumn(
                name: "long_stay_price_per_night",
                table: "pricing_rules");

            migrationBuilder.DropColumn(
                name: "address",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "social_media",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "check_in_time",
                table: "booking_settings");

            migrationBuilder.DropColumn(
                name: "check_out_time",
                table: "booking_settings");

            migrationBuilder.DropColumn(
                name: "extended_check_in_time",
                table: "booking_settings");

            migrationBuilder.DropColumn(
                name: "extended_check_out_time",
                table: "booking_settings");
        }
    }
}
