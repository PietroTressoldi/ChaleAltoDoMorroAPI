namespace ChaleAltoDoMorroAPI.Domain;

public class PricingRule
{
    public int Id { get; set; }
    public PricingKind Kind { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public int? MinNights { get; set; }

    // Só no Weekday: diária de semana quando a estadia tem 2+ diárias (ex.: 645). null = sem desconto.
    public decimal? LongStayPricePerNight { get; set; }
}

public enum PricingKind
{
    Weekday,
    Weekend,
    Extended // diária estendida (sex → sáb ou sáb → dom): PricePerNight é o preço fechado
}
