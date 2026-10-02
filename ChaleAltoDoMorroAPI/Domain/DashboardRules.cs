namespace ChaleAltoDoMorroAPI.Domain;

// Contas do dashboard (task 19) que dá para errar: recorte de noites, ocupação, ticket médio, meses, série e agenda.
public static class DashboardRules
{
    // Quantas noites de [checkIn, checkOut) caem dentro de [from, to] (to inclusivo, como no DashboardPeriod).
    public static int NightsWithin(DateOnly checkIn, DateOnly checkOut, DateOnly from, DateOnly to)
    {
        var endExclusive = to.AddDays(1);

        var start = checkIn > from ? checkIn : from;
        var end = checkOut < endExclusive ? checkOut : endExclusive;

        return Math.Max(end.DayNumber - start.DayNumber, 0);  // estadia fora do período → negativo → 0
    }

    // Dias do período, contando o primeiro e o último.
    public static int DaysIn(DateOnly from, DateOnly to) => to.DayNumber - from.DayNumber + 1;

    // Dias do período − noites com bloqueio manual. A folga não desconta (contrato).
    public static int AvailableNights(DateOnly from, DateOnly to, int manualBlockedNights) =>
        Math.Max(DaysIn(from, to) - manualBlockedNights, 0);

    // nightsSold ÷ availableNights, sempre entre 0 e 1. Sem noite disponível → 0 (sem divisão por zero).
    public static decimal OccupancyRate(int nightsSold, int availableNights)
    {
        if (availableNights <= 0 || nightsSold <= 0) return 0m;

        var rate = (decimal)nightsSold / availableNights;
        return Math.Round(Math.Min(rate, 1m), 3, MidpointRounding.AwayFromZero);
    }

    // reservationRevenue ÷ confirmedReservations. Com 0 reservas → 0.
    public static decimal AverageTicket(decimal revenue, int reservations) =>
        reservations <= 0 ? 0m : Math.Round(revenue / reservations, 2, MidpointRounding.AwayFromZero);

    // O dia 1 de cada mês do período (mesma convenção do StartMonth das despesas fixas).
    public static IReadOnlyList<DateOnly> MonthsOf(DateOnly from, DateOnly to)
    {
        var months = new List<DateOnly>();
        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
            months.Add(m);
        return months;
    }

    // Meses do período, até o mês de hoje, que ainda não têm livro. Mês futuro não é "faltando".
    public static IReadOnlyList<DateOnly> MonthsWithoutBook(DateOnly from, DateOnly to, DateOnly today,
        IReadOnlySet<DateOnly> monthsWithBook)
    {
        var currentMonth = new DateOnly(today.Year, today.Month, 1);
        return MonthsOf(from, to)
            .Where(m => m <= currentMonth && !monthsWithBook.Contains(m))
            .ToList();
    }

    // "2026-11": o formato de mês do contrato. Sem ToString("yyyy-MM") para não depender da cultura do servidor.
    public static string MonthKey(DateOnly month) => $"{month.Year:D4}-{month.Month:D2}";

    private static readonly string[] ShortMonths =
        ["jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"];

    // Fatias do gráfico: uma por dia no "month" ("01/10"), uma por mês nos demais ("out").
    public static IReadOnlyList<SeriesBucket> Buckets(DashboardPeriod period) =>
        period.Kind == PeriodKind.Month
            ? Enumerable.Range(0, DaysIn(period.From, period.To))
                .Select(i => period.From.AddDays(i))
                .Select(d => new SeriesBucket($"{d.Day:D2}/{d.Month:D2}", d, d))
                .ToList()
            : MonthsOf(period.From, period.To)
                .Select(m => new SeriesBucket(ShortMonths[m.Month - 1], m, m.AddMonths(1).AddDays(-1)))
                .ToList();

    // Os números de cada fatia, com as MESMAS regras do "current": faturamento pelo check-in, noites recortadas,
    // ocupação descontando bloqueios manuais. Recebe tudo já filtrado pelo período (o volume é pequeno).
    public static IReadOnlyList<SeriesPoint> Series(IReadOnlyList<SeriesBucket> buckets, IReadOnlyList<StayTotal> stays,
        IReadOnlyList<CashEntry> cash, IReadOnlySet<DateOnly> blocked) =>
        buckets.Select(b =>
        {
            var revenue = stays.Where(s => s.CheckIn >= b.From && s.CheckIn <= b.To).Sum(s => s.Total);
            var nights = stays.Sum(s => NightsWithin(s.CheckIn, s.CheckOut, b.From, b.To));
            var available = AvailableNights(b.From, b.To, blocked.Count(d => d >= b.From && d <= b.To));

            var inBucket = cash.Where(c => c.Date >= b.From && c.Date <= b.To).ToList();
            var income = inBucket.Where(c => c.Type == CashTransactionType.Income).Sum(c => c.Amount);
            var expense = inBucket.Where(c => c.Type == CashTransactionType.Expense).Sum(c => c.Amount);

            return new SeriesPoint(b, revenue, income, expense, income - expense, OccupancyRate(nights, available));
        }).ToList();

    // Próximos check-ins e check-outs a partir de hoje (inclusive), misturados por data.
    // No mesmo dia, quem sai (até 12h) vem antes de quem chega (a partir das 15h).
    public static IReadOnlyList<AgendaEvent> Agenda(IEnumerable<AgendaStay> stays, DateOnly today, int take) =>
        stays.SelectMany(s => new[]
            {
                new AgendaEvent(s.ReservationId, s.GuestName, s.CheckIn, AgendaType.CheckIn),
                new AgendaEvent(s.ReservationId, s.GuestName, s.CheckOut, AgendaType.CheckOut)
            })
            .Where(e => e.Date >= today)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Type == AgendaType.CheckOut ? 0 : 1)
            .Take(take)
            .ToList();
}
