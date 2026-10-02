

namespace ChaleAltoDoMorroAPI.Domain;


// Tipo de arquivo que a API aceita: o content type que vai para o storage e a extensão do nome gerado.
public record FileType(string ContentType, string Extension);

// Descobre o tipo REAL pelos primeiros bytes ("magic numbers"). O ContentType e a extensão vêm do navegador
// e podem mentir: um .exe renomeado para .jpg continua começando com "MZ".

public static class FileSignature
{
    public static readonly FileType Jpeg = new("image/jpeg", ".jpg");
    public static readonly FileType Png = new("image/png", ".png");
    public static readonly FileType Webp = new("image/webp", ".webp");
    public static readonly FileType Mp4 = new("video/mp4", ".mp4");

    public static readonly IReadOnlyList<FileType> Images = [Jpeg, Png, Webp];

    // O WebP é o que precisa de mais bytes: "RIFF" (0–3) + tamanho (4–7) + "WEBP" (8–11).
    public const int HeaderLength = 12;

    private static ReadOnlySpan<byte> JpegMagic => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static FileType? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegMagic))
            return Jpeg;

        if (header.StartsWith(PngMagic))
            return Png;

        if (header.Length >= 12 && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
            return Webp;

        if (header.Length >= 8 && header.Slice(4, 4).SequenceEqual("ftyp"u8))
            return Mp4;

        return null;
    }
}