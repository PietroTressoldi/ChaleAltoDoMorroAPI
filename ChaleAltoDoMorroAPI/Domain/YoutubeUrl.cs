using System.Text.RegularExpressions;

namespace ChaleAltoDoMorroAPI.Domain;


// Mesmos formatos que o site sabe tocar (landing: src/lib/video.ts): watch?v=, youtu.be/, shorts/, embed/ + id de 11.
public static partial class YoutubeUrl
{
    public static bool IsValid(string url) => Pattern().IsMatch(url);

    [GeneratedRegex(@"^https://(www\.|m\.)?(youtube\.com/(watch\?v=|shorts/|embed/)|youtu\.be/)[A-Za-z0-9_-]{11}([?&#/].*)?$")]
    private static partial Regex Pattern();
}