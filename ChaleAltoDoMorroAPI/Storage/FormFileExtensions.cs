using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Storage;

public static class FormFileExtensions
{
    // Lê só o começo do arquivo para descobrir o tipo REAL. O ContentType e a extensão vêm do navegador.
    public static async Task<FileType?> DetectTypeAsync(this IFormFile file, CancellationToken ct = default)
    {
        var header = new byte[FileSignature.HeaderLength];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        return FileSignature.Detect(header.AsSpan(0, read));
    }
}
