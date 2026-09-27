namespace ChaleAltoDoMorroAPI.Domain;

public class GalleryImage
{
    public Guid Id { get; set; }
    public string Src { get; set; } = string.Empty;
    public string? SrcSet { get; set; }
    public string Alt { get; set; } = string.Empty;
    public int Order { get; set; }
}
