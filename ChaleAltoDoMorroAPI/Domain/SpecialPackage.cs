namespace ChaleAltoDoMorroAPI.Domain;

public class SpecialPackage
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public decimal Price { get; set; }
}
