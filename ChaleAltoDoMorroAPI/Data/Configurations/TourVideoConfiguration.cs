using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class TourVideoConfiguration : IEntityTypeConfiguration<TourVideo>
{
    public void Configure(EntityTypeBuilder<TourVideo> builder)
    {
        builder.ToTable("tour_videos");

        builder.HasKey(video => video.Id);

        builder.Property(video => video.Title)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(video => video.Description)
            .HasMaxLength(1000);

        builder.Property(video => video.Url)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(video => video.PosterUrl)
            .HasMaxLength(500);

        builder.Property(video => video.Orientation)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
    }
}
