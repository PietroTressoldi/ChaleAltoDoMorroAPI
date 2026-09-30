using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class RecurringExpenseConfiguration : IEntityTypeConfiguration<RecurringExpense>
{
    public void Configure(EntityTypeBuilder<RecurringExpense> builder)
    {
        builder.ToTable("recurring_expenses", table =>
        {
            table.HasCheckConstraint("ck_recurring_expenses_amount_positive", "amount > 0");
            table.HasCheckConstraint("ck_recurring_expenses_day_range", "day_of_month BETWEEN 1 AND 31");
            table.HasCheckConstraint("ck_recurring_expenses_end_after_start", "end_month IS NULL OR end_month >= start_month");
        });

        builder.HasKey(expense => expense.Id);

        builder.Property(expense => expense.Description)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(expense => expense.Amount)
            .HasPrecision(12, 2);

        builder.Property(expense => expense.PaymentMethod)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasOne<CashCategory>()
            .WithMany()
            .HasForeignKey(expense => expense.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}