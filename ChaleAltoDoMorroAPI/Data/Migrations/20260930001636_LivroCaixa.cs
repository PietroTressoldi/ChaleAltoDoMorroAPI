using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChaleAltoDoMorroAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class LivroCaixa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_books",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    year = table.Column<int>(type: "integer", nullable: false),
                    month = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_books", x => x.id);
                    table.CheckConstraint("ck_cash_books_month_range", "month BETWEEN 1 AND 12");
                    table.CheckConstraint("ck_cash_books_year_range", "year BETWEEN 2020 AND 2100");
                });

            migrationBuilder.CreateTable(
                name: "cash_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recurring_expenses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    day_of_month = table.Column<int>(type: "integer", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    start_month = table.Column<DateOnly>(type: "date", nullable: false),
                    end_month = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_expenses", x => x.id);
                    table.CheckConstraint("ck_recurring_expenses_amount_positive", "amount > 0");
                    table.CheckConstraint("ck_recurring_expenses_day_range", "day_of_month BETWEEN 1 AND 31");
                    table.CheckConstraint("ck_recurring_expenses_end_after_start", "end_month IS NULL OR end_month >= start_month");
                    table.ForeignKey(
                        name: "fk_recurring_expenses_cash_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "cash_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cash_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cash_book_id = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recurring_expense_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_transactions", x => x.id);
                    table.CheckConstraint("ck_cash_transactions_amount_positive", "amount > 0");
                    table.ForeignKey(
                        name: "fk_cash_transactions_cash_books_cash_book_id",
                        column: x => x.cash_book_id,
                        principalTable: "cash_books",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_transactions_cash_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "cash_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_transactions_recurring_expenses_recurring_expense_id",
                        column: x => x.recurring_expense_id,
                        principalTable: "recurring_expenses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cash_books_year_month",
                table: "cash_books",
                columns: new[] { "year", "month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cash_transactions_cash_book_id_recurring_expense_id",
                table: "cash_transactions",
                columns: new[] { "cash_book_id", "recurring_expense_id" },
                unique: true,
                filter: "recurring_expense_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_cash_transactions_category_id",
                table: "cash_transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_transactions_date",
                table: "cash_transactions",
                column: "date");

            migrationBuilder.CreateIndex(
                name: "ix_cash_transactions_recurring_expense_id",
                table: "cash_transactions",
                column: "recurring_expense_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_expenses_category_id",
                table: "recurring_expenses",
                column: "category_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_transactions");

            migrationBuilder.DropTable(
                name: "cash_books");

            migrationBuilder.DropTable(
                name: "recurring_expenses");

            migrationBuilder.DropTable(
                name: "cash_categories");
        }
    }
}
