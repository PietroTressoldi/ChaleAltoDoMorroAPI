using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Dtos;

public record PricingRuleDto(PricingKind Kind, string Label, decimal PricePerNight, int? MinNights);
