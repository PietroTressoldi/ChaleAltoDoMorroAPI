using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class TourVideoConfiguration : IEntityTypeConfiguration<TourVideo>
{
    public void Configure(EntityTypeBuilder<TourVideo> builder)
    {
        builder.ToTable("tour_videos", table =>
              // Ou é link do YouTube, ou é arquivo nosso: nunca os dois, nunca nenhum.
              table.HasCheckConstraint("ck_tour_videos_url_xor_file", "(url IS NULL) <> (file_key IS NULL)"));

        builder.Property(video => video.Url)
            .HasMaxLength(500);       // sem IsRequired: vídeo enviado não tem link

        builder.Property(video => video.FileKey)
            .HasMaxLength(200);

        builder.Property(video => video.PosterKey)
            .HasMaxLength(200);

        builder.HasKey(video => video.Id);

        builder.Property(video => video.Title)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(video => video.Description)
            .HasMaxLength(1000);

        builder.Property(video => video.Orientation)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
    }
}
