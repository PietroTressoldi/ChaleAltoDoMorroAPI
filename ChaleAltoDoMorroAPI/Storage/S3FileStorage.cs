using Amazon.S3;
using Amazon.S3.Model;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.Extensions.Options;

namespace ChaleAltoDoMorroAPI.Storage;

// Arquivos no S3, servidos pelo CloudFront. A chave tem o mesmo formato do LocalFileStorage ("galeria/3f2a....jpg"),
// então migrar é só copiar a pasta para o bucket (aws s3 sync) sem mexer no banco.
public class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage
{
    private readonly string _bucket = options.Value.BucketName;
    private readonly string _publicBaseUrl = options.Value.PublicBaseUrl.TrimEnd('/') + "/";

    public async Task<string> SaveAsync(Stream content, FileType type, string folder, CancellationToken ct = default)
    {
        // Nome gerado: o nome que veio do navegador nunca vira chave no bucket.
        var key = $"{folder}/{Guid.NewGuid():N}{type.Extension}";

        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = type.ContentType   // o CloudFront devolve este Content-Type para o navegador
        };

        // Arquivo nunca muda depois de enviado (trocar = nova chave), então o navegador e o CloudFront podem guardar por 1 ano.
        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await s3.PutObjectAsync(request, ct);
        return key;
    }

    // No S3, apagar uma chave que não existe não dá erro (mesmo comportamento do LocalFileStorage).
    public async Task DeleteAsync(string key, CancellationToken ct = default) =>
        await s3.DeleteObjectAsync(_bucket, key, ct);

    public string GetPublicUrl(string key) => _publicBaseUrl + key;
}
