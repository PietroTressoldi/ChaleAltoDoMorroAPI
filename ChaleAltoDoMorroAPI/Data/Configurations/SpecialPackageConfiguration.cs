using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class SpecialPackageConfiguration : IEntityTypeConfiguration<SpecialPackage>
{
    public void Configure(EntityTypeBuilder<SpecialPackage> builder)
    {
        builder.ToTable("special_packages", table =>
        {
            table.HasCheckConstraint("ck_special_packages_checkout_after_checkin", "check_out > check_in");
            table.HasCheckConstraint("ck_special_packages_price_positive", "price > 0");
        });

        builder.HasKey(package => package.Id);

        builder.Property(package => package.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(package => package.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(package => package.Price)
            .HasPrecision(10, 2);

        builder.HasIndex(package => package.CheckIn);
    }
}
