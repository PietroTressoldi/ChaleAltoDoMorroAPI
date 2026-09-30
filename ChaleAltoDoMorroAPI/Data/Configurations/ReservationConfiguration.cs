using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations", table =>
        {
            table.HasCheckConstraint("ck_reservations_checkout_after_checkin", "check_out > check_in");
            table.HasCheckConstraint("ck_reservations_nights_positive", "nights > 0");
            table.HasCheckConstraint("ck_reservations_buffer_before_range", "buffer_before_nights IS NULL OR buffer_before_nights BETWEEN 0 AND 3");
            table.HasCheckConstraint("ck_reservations_buffer_after_range", "buffer_after_nights IS NULL OR buffer_after_nights BETWEEN 0 AND 3");
        });

        builder.HasKey(reservation => reservation.Id);

        builder.Property(reservation => reservation.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(reservation => reservation.NightsTotal).HasPrecision(10, 2);
        builder.Property(reservation => reservation.Total).HasPrecision(10, 2);
        builder.Property(reservation => reservation.CalculatedTotal).HasPrecision(10, 2);
        builder.Property(reservation => reservation.EstimatedTotalFromSite).HasPrecision(10, 2);

        builder.Property(reservation => reservation.Source)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(reservation => reservation.Observations)
            .HasMaxLength(1000);

        builder.Property(reservation => reservation.AdminNotes)
            .HasMaxLength(2000);

        // Snapshot do pacote: o nome fica gravado mesmo que o pacote mude ou seja apagado depois.
        builder.Property(reservation => reservation.PackageName)
            .HasMaxLength(120);

        builder.HasOne<SpecialPackage>()
            .WithMany()
            .HasForeignKey(reservation => reservation.PackageId)
            .OnDelete(DeleteBehavior.SetNull);

        // Snapshot do kit (um por reserva): nome e preço do momento da reserva.
        builder.Property(reservation => reservation.KitName)
            .HasMaxLength(120);

        builder.Property(reservation => reservation.KitPrice)
            .HasPrecision(10, 2);

        builder.HasOne<Kit>()
            .WithMany()
            .HasForeignKey(reservation => reservation.KitId)
            .OnDelete(DeleteBehavior.SetNull);

        // Dois relacionamentos com a mesma tabela guests: por isso cada um precisa da FK explícita.
        // Restrict: não dá para apagar um hóspede que tem reserva (para LGPD, anonimizar em vez de apagar).
        // A convenção snake_case geraria "guest1id" (não separa depois de número).
        builder.Property(reservation => reservation.Guest1Id).HasColumnName("guest1_id");
        builder.Property(reservation => reservation.Guest2Id).HasColumnName("guest2_id");

        builder.HasOne(reservation => reservation.Guest1)
            .WithMany()
            .HasForeignKey(reservation => reservation.Guest1Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(reservation => reservation.Guest2)
            .WithMany()
            .HasForeignKey(reservation => reservation.Guest2Id)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(reservation => reservation.ContactPhone)
            .HasMaxLength(11)
            .IsRequired();

        builder.Property(reservation => reservation.ContactEmail)
            .HasMaxLength(254);

        builder.HasIndex(reservation => new { reservation.Status, reservation.CheckIn });
    }
}
