using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class BookingSettingsConfiguration : IEntityTypeConfiguration<BookingSettings>
{
    public void Configure(EntityTypeBuilder<BookingSettings> builder)
    {
        builder.ToTable("booking_settings", table =>
        {
            table.HasCheckConstraint("ck_booking_settings_single_row", "id = 1");
            table.HasCheckConstraint("ck_booking_settings_buffer_before_range", "buffer_before_nights BETWEEN 0 AND 3");
            table.HasCheckConstraint("ck_booking_settings_buffer_after_range", "buffer_after_nights BETWEEN 0 AND 3");
        });

        builder.HasKey(settings => settings.Id);

        // Id fixo: o banco não gera (senão a linha única poderia virar id 2).
        builder.Property(settings => settings.Id).ValueGeneratedNever();

        // A linha nasce COM a migration, em todos os ambientes: padrão 0/0 (o comportamento de antes da task 16).
        builder.HasData(new BookingSettings { Id = BookingSettings.SingletonId, BufferBeforeNights = 0, BufferAfterNights = 0 });
    }
}