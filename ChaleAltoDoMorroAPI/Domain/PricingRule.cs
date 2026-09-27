namespace ChaleAltoDoMorroAPI.Domain;

public class PricingRule
{
    public int Id { get; set; }
    public PricingKind Kind { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public int? MinNights { get; set; }
}

public enum PricingKind
{
    Weekday,
    Weekend
}
