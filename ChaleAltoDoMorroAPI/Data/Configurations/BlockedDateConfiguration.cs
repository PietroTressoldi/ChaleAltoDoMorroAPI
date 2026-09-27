using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class BlockedDateConfiguration : IEntityTypeConfiguration<BlockedDate>
{
    public void Configure(EntityTypeBuilder<BlockedDate> builder)
    {
        builder.ToTable("blocked_dates");

        builder.HasKey(blocked => blocked.Id);

        builder.Property(blocked => blocked.Reason)
            .HasMaxLength(200);

        builder.HasIndex(blocked => blocked.Date)
            .IsUnique();
    }
}
