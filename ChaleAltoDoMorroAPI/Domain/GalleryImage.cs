namespace ChaleAltoDoMorroAPI.Domain;


public class GalleryImage
{
    public Guid Id { get; set; }
    public string FileKey { get; set; } = string.Empty;   // chave no IFileStorage, ex.: "galeria/3f2a....webp"
    public string Alt { get; set; } = string.Empty;
    public int Order { get; set; }
}