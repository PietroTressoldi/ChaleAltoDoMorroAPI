namespace ChaleAltoDoMorroAPI.Domain;

// Tudo o que as regras de reserva precisam saber, já carregado (pelo service, na task 5).
// Não usa as entidades do EF de propósito: as regras não dependem de como os dados são guardados.
public record BookingData(
    decimal? WeekdayPrice,          // null = sem regra "weekday" cadastrada (não dá para orçar)
    decimal? WeekendPrice,          // null = sem regra "weekend" (sex, sáb e dom usam o preço de semana)
    int? WeekendMinNights,          // null = padrão 2
    IReadOnlySet<DateOnly> BlockedNights,
    IReadOnlyList<PackageInfo> Packages,
    decimal? WeekdayLongStayPrice = null, // diária de semana em estadia de 2+ diárias; null = sem desconto
    decimal? ExtendedPrice = null);       // preço fechado da diária estendida; null = não existe

public record PackageInfo(Guid Id, string Name, DateOnly CheckIn, DateOnly CheckOut, decimal Price);

// Kit escolhido na reserva (um por reserva): só o que o orçamento precisa.
public record KitInfo(Guid Id, string Name, decimal Price);
