namespace ChaleAltoDoMorroAPI.Domain;

public enum NightKind
{
    Weekday,
    Weekend,
    Package,
    Extended
}

/// <summary>Preço de uma noite. No pacote, é o total dividido pelas diárias (só como referência).</summary>
public record NightPrice(DateOnly Date, NightKind Kind, decimal Price, Guid? PackageId = null);

/// <summary>Noites agrupadas para o resumo: "2 diárias de fim de semana" ou "Pacote Finados (3 diárias)".</summary>
/// <param name="UnitPrice">Preço por diária; null no pacote (o preço é do pacote fechado).</param>
public record QuoteLine(NightKind Kind, string? Detail, int Count, decimal? UnitPrice, decimal Subtotal);

/// <summary>Orçamento de uma estadia. É o que a API grava na reserva, recalculado no servidor.</summary>
public record StayQuote(
    DateOnly CheckIn,
    DateOnly CheckOut,
    IReadOnlyList<NightPrice> Nights,
    IReadOnlyList<QuoteLine> Lines,
    decimal NightsTotal,
    KitInfo? Kit,
    decimal KitTotal,
    decimal Total,
    PackageInfo? Package,
    bool IncludesColonialBreakfast);
