using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class PricingRuleConfiguration : IEntityTypeConfiguration<PricingRule>
{
    public void Configure(EntityTypeBuilder<PricingRule> builder)
    {
        builder.ToTable("pricing_rules", table =>
            table.HasCheckConstraint("ck_pricing_rules_price_positive", "price_per_night > 0"));

        builder.HasKey(rule => rule.Id);

        builder.Property(rule => rule.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(rule => rule.Label)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(rule => rule.PricePerNight)
            .HasPrecision(10, 2);

        builder.HasIndex(rule => rule.Kind)
            .IsUnique();
    }
}
