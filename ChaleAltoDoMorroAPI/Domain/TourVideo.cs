namespace ChaleAltoDoMorroAPI.Domain;

public class TourVideo
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? PosterUrl { get; set; }
    public VideoOrientation Orientation { get; set; }
    public int Order { get; set; }
}

public enum VideoOrientation
{
    Landscape,
    Portrait
}
