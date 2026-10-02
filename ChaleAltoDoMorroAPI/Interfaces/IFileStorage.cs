using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Interfaces;


// Onde moram os arquivos (fotos, vídeos). Hoje é o disco local; amanhã, o S3.
// Quem usa só conhece a CHAVE ("galeria/3f2a....webp"). A URL é montada na hora de responder.
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, FileType type, string folder, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    string GetPublicUrl(string key);
}