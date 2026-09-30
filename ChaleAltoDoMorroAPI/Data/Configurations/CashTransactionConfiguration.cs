using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class CashTransactionConfiguration : IEntityTypeConfiguration<CashTransaction>
{
    public void Configure(EntityTypeBuilder<CashTransaction> builder)
    {
        builder.ToTable("cash_transactions", table =>
            table.HasCheckConstraint("ck_cash_transactions_amount_positive", "amount > 0"));

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(transaction => transaction.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(transaction => transaction.Amount)
            .HasPrecision(12, 2);

        builder.Property(transaction => transaction.Description)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(transaction => transaction.Notes)
            .HasMaxLength(500);

        // Restrict: categoria com transação não pode sumir, senão o histórico fica sem categoria.
        // Por isso "apagar categoria" com uso só desativa (regra 18).
        builder.HasOne(transaction => transaction.Category)
            .WithMany()
            .HasForeignKey(transaction => transaction.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull: apagar a despesa fixa mantém as transações lançadas, só solta o vínculo (regra 16).
        builder.HasOne<RecurringExpense>()
            .WithMany()
            .HasForeignKey(transaction => transaction.RecurringExpenseId)
            .OnDelete(DeleteBehavior.SetNull);

        // Regra 13: a mesma despesa fixa só entra uma vez por livro.
        // O filtro é essencial: sem ele, todas as transações manuais (NULL) do mesmo livro colidiriam.
        builder.HasIndex(transaction => new { transaction.CashBookId, transaction.RecurringExpenseId })
            .IsUnique()
            .HasFilter("recurring_expense_id IS NOT NULL");

        // Consultas por período (dashboard e visão anual).
        builder.HasIndex(transaction => transaction.Date);
    }
}