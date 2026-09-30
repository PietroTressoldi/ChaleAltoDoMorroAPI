using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class CashBookConfiguration : IEntityTypeConfiguration<CashBook>
{
    public void Configure(EntityTypeBuilder<CashBook> builder)
    {
        builder.ToTable("cash_books", table =>
        {
            table.HasCheckConstraint("ck_cash_books_month_range", "month BETWEEN 1 AND 12");
            table.HasCheckConstraint("ck_cash_books_year_range", "year BETWEEN 2020 AND 2100");
        });

        builder.HasKey(book => book.Id);

        builder.Property(book => book.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(book => book.Notes)
            .HasMaxLength(1000);

        // Regra 1: um livro por mês.
        builder.HasIndex(book => new { book.Year, book.Month })
            .IsUnique();

        // Restrict: o banco recusa apagar livro com transações (regra 6). Cascade apagaria o mês inteiro sem aviso.
        builder.HasMany(book => book.Transactions)
            .WithOne()
            .HasForeignKey(transaction => transaction.CashBookId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
