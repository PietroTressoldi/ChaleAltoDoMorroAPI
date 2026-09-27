namespace ChaleAltoDoMorroAPI.Dtos;

public record SpecialPackageDto(Guid Id, string Name, string Description, DateOnly CheckIn, DateOnly CheckOut, decimal Price);
