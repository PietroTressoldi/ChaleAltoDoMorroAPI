using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.ToTable("guests");

        builder.HasKey(guest => guest.Id);

        builder.Property(guest => guest.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(guest => guest.Cpf)
            .HasMaxLength(11);

        builder.Property(guest => guest.PhoneNumber)
            .HasMaxLength(11);

        builder.Property(guest => guest.Email)
            .HasMaxLength(254);

        // Único, mas aceita vários NULL (no Postgres, NULL não conta como repetido).
        builder.HasIndex(guest => guest.Cpf)
            .IsUnique();
    }
}
