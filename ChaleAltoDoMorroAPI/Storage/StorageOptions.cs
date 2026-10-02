namespace ChaleAltoDoMorroAPI.Storage;

// Onde os arquivos moram. Local = wwwroot/uploads servido pela própria API (dev). S3 = bucket atrás do CloudFront (produção).
public enum StorageProvider
{
    Local,
    S3
}

public class StorageOptions
{
    public StorageProvider Provider { get; set; } = StorageProvider.Local;

    // De onde o navegador baixa os arquivos. Dev: http://localhost:5084/uploads/. Produção (S3): o domínio do CloudFront.
    // Trocar aqui muda todas as URLs da resposta sem mexer no banco.
    public string PublicBaseUrl { get; set; } = string.Empty;

    // Só no S3. A região e as credenciais NÃO ficam aqui: vêm da seção "AWS" / AWS_REGION e da IAM Role da EC2.
    public string BucketName { get; set; } = string.Empty;
}
