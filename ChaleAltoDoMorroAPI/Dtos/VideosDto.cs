using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Dtos;

public record VideosDto(Guid Id, string Title, string? Description, string Url, string? PosterUrl, VideoOrientation Orientation, int Order);
