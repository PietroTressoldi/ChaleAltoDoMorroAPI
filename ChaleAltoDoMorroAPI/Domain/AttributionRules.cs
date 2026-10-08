using System.Globalization;

namespace ChaleAltoDoMorroAPI.Domain;

// Origem da pré-reserva (de onde o visitante veio: anúncio da Meta, Instagram, Google...), já limpa.
public record ReservationAttribution(
    string Source,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmContent,
    string? UtmTerm,
    string? Fbclid,
    DateTimeOffset? FirstVisitAt);

// Regra de ouro: atribuição é dado de marketing e NUNCA derruba uma pré-reserva.
// Por isso aqui nada é recusado: valor estranho vira "other", texto vazio vira null, data ruim vira null.
// Classe pura (sem EF, sem HTTP), igual ao BookingRules: dá para testar sem banco.
public static class AttributionRules
{
    public const int MaxUtmLength = 150;
    // O fbclid é gerado pela Meta (não digitado) e passa de 150 com folga: ~60 a 250 caracteres.
    public const int MaxFbclidLength = 500;
    public const string Other = "other";

    // Mesmos valores que o site manda (landing: src/lib/attribution.ts).
    public static readonly IReadOnlySet<string> KnownSources =
        new HashSet<string>(StringComparer.Ordinal) { "meta_ads", "instagram", "google", "direct", Other };

    public static ReservationAttribution Normalize(
        string? source, string? utmSource, string? utmMedium, string? utmCampaign, string? utmContent,
        string? utmTerm, string? fbclid, string? firstVisitAt) =>
        new(
            NormalizeSource(source),
            CleanText(utmSource),
            CleanText(utmMedium),
            CleanText(utmCampaign),
            CleanText(utmContent),
            CleanText(utmTerm),
            CleanText(fbclid, MaxFbclidLength),
            ParseUtc(firstVisitAt));

    // Fora da lista (ou vazio) → "other". Comparação exata depois de Trim + minúsculas.
    public static string NormalizeSource(string? source)
    {
        var value = source?.Trim().ToLowerInvariant();
        return value is not null && KnownSources.Contains(value) ? value : Other;
    }

    // Os UTMs são digitados à mão nos links dos anúncios: tira espaços, vazio vira null e corta no limite da coluna.
    public static string? CleanText(string? text, int maxLength = MaxUtmLength)
    {
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value))
            return null;

        return value.Length > maxLength ? value[..maxLength] : value;
    }

    // Chega como texto (e não DateTimeOffset no DTO) de propósito: se o model binding tentasse converter
    // uma data inválida, devolveria 400 e a pré-reserva se perderia. Aqui, data ruim só vira null.
    // Sempre em UTC: o Npgsql só grava DateTimeOffset com offset zero numa coluna timestamptz.
    public static DateTimeOffset? ParseUtc(string? isoDate)
    {
        if (string.IsNullOrWhiteSpace(isoDate))
            return null;

        return DateTimeOffset.TryParse(isoDate.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }
}
