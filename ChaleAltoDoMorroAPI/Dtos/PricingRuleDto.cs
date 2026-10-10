using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Dtos;

// LongStayPricePerNight só existe no weekday (null some do JSON).
public record PricingRuleDto(PricingKind Kind, string Label, decimal PricePerNight, int? MinNights, decimal? LongStayPricePerNight);
