namespace ChaleAltoDoMorroAPI.Domain;

public class TourVideo
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Url { get; set; }        // link do YouTube (não é arquivo nosso)
    public string? FileKey { get; set; }    // mp4 enviado pelo portal
    public string? PosterKey { get; set; }  // capa enviada pelo portal
    public VideoOrientation Orientation { get; set; }
    public int Order { get; set; }
}

public enum VideoOrientation
{
    Landscape,
    Portrait
}
