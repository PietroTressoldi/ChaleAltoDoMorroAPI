namespace ChaleAltoDoMorroAPI.Dtos;

public record KitDto(Guid Id, string Name, string? Description, decimal Price, List<string> Items);
