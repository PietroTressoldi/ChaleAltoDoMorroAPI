namespace ChaleAltoDoMorroAPI.Domain;

// Tudo o que as regras de reserva precisam saber, já carregado (pelo service, na task 5).
// Não usa as entidades do EF de propósito: as regras não dependem de como os dados são guardados.
public record BookingData(
    decimal? WeekdayPrice,          // null = sem regra "weekday" cadastrada (não dá para orçar)
    decimal? WeekendPrice,          // null = sem regra "weekend" (sexta/sábado usam o preço de semana)
    int? WeekendMinNights,          // null = padrão 2
    IReadOnlySet<DateOnly> BlockedNights,
    IReadOnlyList<PackageInfo> Packages);

public record PackageInfo(Guid Id, string Name, DateOnly CheckIn, DateOnly CheckOut, decimal Price);

// Kit escolhido na reserva (um por reserva): só o que o orçamento precisa.
public record KitInfo(Guid Id, string Name, decimal Price);
