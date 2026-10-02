using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.Extensions.Options;

namespace ChaleAltoDoMorroAPI.Storage;

public class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly string _publicBaseUrl;

    public LocalFileStorage(IWebHostEnvironment env, IOptions<StorageOptions> options)
    {
        _root = RootPath(env);
        _publicBaseUrl = options.Value.PublicBaseUrl.TrimEnd('/') + "/";
    }

    // O Program.cs usa o mesmo caminho para servir os arquivos em /uploads.
    public static string RootPath(IWebHostEnvironment env) =>
        Path.GetFullPath(Path.Combine(env.ContentRootPath, "wwwroot", "uploads"));

    public async Task<string> SaveAsync(Stream content, FileType type, string folder, CancellationToken ct = default)
    {
        // Nome gerado: o nome que veio do navegador nunca chega ao disco.
        var key = $"{folder}/{Guid.NewGuid():N}{type.Extension}";
        var path = FullPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            await content.CopyToAsync(file, ct);
        }
        catch
        {
            // Upload cancelado ou com erro no meio: não deixa arquivo pela metade no disco.
            File.Delete(path);
            throw;
        }

        return key;
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = FullPath(key);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key) => _publicBaseUrl + key;

    // Chave → caminho no disco, sem deixar sair de wwwroot/uploads (ex.: "../../appsettings.json").
    private string FullPath(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Chave de arquivo inválida.", nameof(key));

        return path;
    }
}