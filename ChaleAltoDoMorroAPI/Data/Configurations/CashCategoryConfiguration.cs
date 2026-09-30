using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class CashCategoryConfiguration : IEntityTypeConfiguration<CashCategory>
{
    public void Configure(EntityTypeBuilder<CashCategory> builder)
    {
        builder.ToTable("cash_categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .HasMaxLength(60)
            .IsRequired();

        builder.Property(category => category.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // "#RRGGBB"
        builder.Property(category => category.Color)
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(category => category.Icon)
            .HasMaxLength(50)
            .IsRequired();
    }
}
