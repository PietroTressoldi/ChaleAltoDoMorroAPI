namespace ChaleAltoDoMorroAPI.Domain;

// Tipos das contas puras da série e da agenda do dashboard (task 19). Sem EF: o serviço busca e entrega prontos.

// Uma fatia do gráfico: um dia (período "month") ou um mês (os demais).
public record SeriesBucket(string Label, DateOnly From, DateOnly To);

public record StayTotal(DateOnly CheckIn, DateOnly CheckOut, decimal Total);

public record CashEntry(DateOnly Date, CashTransactionType Type, decimal Amount);

public record SeriesPoint(
    SeriesBucket Bucket, decimal ReservationRevenue, decimal CashIncome, decimal CashExpense, decimal CashNet,
    decimal OccupancyRate);

public enum AgendaType
{
    CheckIn,
    CheckOut
}

public record AgendaStay(Guid ReservationId, string GuestName, DateOnly CheckIn, DateOnly CheckOut);

public record AgendaEvent(Guid ReservationId, string GuestName, DateOnly Date, AgendaType Type);
