using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class KitConfiguration : IEntityTypeConfiguration<Kit>
{
    public void Configure(EntityTypeBuilder<Kit> builder)
    {
        builder.ToTable("kits", table =>
            table.HasCheckConstraint("ck_kits_price_positive", "price > 0"));

        builder.HasKey(kit => kit.Id);

        builder.Property(kit => kit.Name)
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(kit => kit.Description)
            .HasMaxLength(500);

        builder.Property(kit => kit.Price)
            .HasPrecision(10, 2);

        // List<string> vira text[] no Postgres (o Npgsql faz o mapeamento sozinho).
        builder.Property(kit => kit.Items)
            .IsRequired();
    }
}
