using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChaleAltoDoMorroAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReservaPeloPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "estimated_total_from_site",
                table: "reservations",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddColumn<decimal>(
                name: "calculated_total",
                table: "reservations",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "reservations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Site"); // (ajustado à mão) toda reserva que já existe veio do site

            // (ajustado à mão) Dados que já existem:
            // - nas reservas do site o preço sempre foi o calculado;
            // - 0 era o "site não mandou estimativa" de antes; agora isso é null.
            migrationBuilder.Sql("UPDATE reservations SET calculated_total = total;");
            migrationBuilder.Sql("UPDATE reservations SET estimated_total_from_site = NULL WHERE estimated_total_from_site = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "calculated_total",
                table: "reservations");

            migrationBuilder.DropColumn(
                name: "source",
                table: "reservations");

            // (ajustado à mão) A coluna volta a ser obrigatória: null vira 0, como era antes.
            migrationBuilder.Sql("UPDATE reservations SET estimated_total_from_site = 0 WHERE estimated_total_from_site IS NULL;");

            migrationBuilder.AlterColumn<decimal>(
                name: "estimated_total_from_site",
                table: "reservations",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
