using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChaleAltoDoMorroAPI.Data.Configurations;

public class GalleryImageConfiguration : IEntityTypeConfiguration<GalleryImage>
{
    public void Configure(EntityTypeBuilder<GalleryImage> builder)
    {
        builder.ToTable("gallery_images");

        builder.HasKey(image => image.Id);

        builder.Property(image => image.Src)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(image => image.SrcSet)
            .HasMaxLength(2000);

        builder.Property(image => image.Alt)
            .HasMaxLength(250)
            .IsRequired();
    }
}
