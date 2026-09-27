namespace ChaleAltoDoMorroAPI.Dtos;

public record GalleryImageDto(Guid Id, string Src, string? SrcSet, string Alt, int Order);
