namespace ChaleAltoDoMorroAPI.Storage;


public class StorageOptions
{
    // De onde o navegador baixa os arquivos. Dev: http://localhost:5084/uploads/. Produção (S3): o domínio do CloudFront.
    // Trocar aqui muda todas as URLs da resposta sem mexer no banco.
    public string PublicBaseUrl { get; set; } = string.Empty;
}
