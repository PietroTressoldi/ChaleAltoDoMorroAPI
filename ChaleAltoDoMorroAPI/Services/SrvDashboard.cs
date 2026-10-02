using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// Dashboard do portal (task 19): busca e soma no banco; as contas que dá para errar ficam no DashboardRules.
public class SrvDashboard(AppDbContext db, IClock clock)
{
    private const int TopExpensesCount = 5;
    private const int UpcomingCount = 10;

    public async Task<DashboardDto> Get(DashboardPeriod period, CancellationToken ct = default)
    {
        var previous = period.Previous();

        return new DashboardDto(
            new PeriodDto(period.Kind, period.Year, period.Index, period.From, period.To, period.Label),
            await Metrics(period.From, period.To, ct),
            await Metrics(previous.From, previous.To, ct),
            await Series(period, ct),
            await ExpensesByCategory(period.From, period.To, ct),
            await TopKits(period.From, period.To, ct),
            await Cash(period.From, period.To, ct),
            await Upcoming(ct),
            await db.Reservations.CountAsync(r => r.Status == ReservationStatus.Pending, ct));
    }

    // Os números de um intervalo [from, to], com to inclusivo.
    private async Task<DashboardMetricsDto> Metrics(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var confirmed = db.Reservations.AsNoTracking().Where(r => r.Status == ReservationStatus.Confirmed);

        // Faturamento e contagem pelo CHECK-IN dentro do período: a estadia conta inteira no mês do check-in.
        var byCheckIn = confirmed.Where(r => r.CheckIn >= from && r.CheckIn <= to);
        var count = await byCheckIn.CountAsync(ct);
        var revenue = await byCheckIn.SumAsync(r => r.Total, ct);

        // Noites vendidas: toda estadia que TOCA o período, recortada a ele. O recorte é C# puro (não vira SQL),
        // então o banco devolve só as duas datas das estadias que interessam, não a reserva inteira.
        var end = to.AddDays(1);
        var stays = await confirmed
            .Where(r => r.CheckIn < end && from < r.CheckOut)
            .Select(r => new { r.CheckIn, r.CheckOut })
            .ToListAsync(ct);
        var nightsSold = stays.Sum(s => DashboardRules.NightsWithin(s.CheckIn, s.CheckOut, from, to));

        var blocked = await db.BlockedDates.CountAsync(b => b.Date >= from && b.Date <= to, ct);
        var available = DashboardRules.AvailableNights(from, to, blocked);

        // Caixa: a data da transação sempre cai no mês do livro (CashBookRules.ValidateTransaction),
        // então somar por data dentro do período = somar os livros dos meses do período.
        var cash = db.CashTransactions.AsNoTracking().Where(t => t.Date >= from && t.Date <= to);
        var income = await cash.Where(t => t.Type == CashTransactionType.Income).SumAsync(t => t.Amount, ct);
        var expense = await cash.Where(t => t.Type == CashTransactionType.Expense).SumAsync(t => t.Amount, ct);

        return new DashboardMetricsDto(
            revenue, count, nightsSold, available,
            DashboardRules.OccupancyRate(nightsSold, available),
            DashboardRules.AverageTicket(revenue, count),
            income, expense, income - expense);
    }

    // Bloco "cash": o que o dono precisa olhar no Livro Caixa do período (e do mês atual, nas despesas fixas).
    private async Task<DashboardCashDto> Cash(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var today = clock.Today();

        // Uma consulta serve para dois campos: os livros que EXISTEM no período (→ monthsWithoutBook)
        // e o líquido de cada um (→ negativeMonths). Ano*12 + mês vira um número que cresce sem "pular"
        // na virada do ano, então dá para filtrar o intervalo com >= e <=.
        var startKey = from.Year * 12 + from.Month;
        var endKey = to.Year * 12 + to.Month;
        var books = await db.CashBooks.AsNoTracking()
            .Where(b => b.Year * 12 + b.Month >= startKey && b.Year * 12 + b.Month <= endKey)
            .OrderBy(b => b.Year).ThenBy(b => b.Month)
            .Select(b => new
            {
                Month = new DateOnly(b.Year, b.Month, 1),
                Net = b.Transactions.Sum(t => t.Type == CashTransactionType.Income ? t.Amount : -t.Amount)
            })
            .ToListAsync(ct);

        var withBook = books.Select(b => b.Month).ToHashSet();
        var monthsWithoutBook = DashboardRules.MonthsWithoutBook(from, to, today, withBook)
            .Select(DashboardRules.MonthKey).ToList();
        var negativeMonths = books.Where(b => b.Net < 0)
            .Select(b => DashboardRules.MonthKey(b.Month)).ToList();

        var topExpenses = await db.CashTransactions.AsNoTracking()
            .Where(t => t.Type == CashTransactionType.Expense && t.Date >= from && t.Date <= to)
            .OrderByDescending(t => t.Amount).ThenBy(t => t.Date)
            .Take(TopExpensesCount)
            .Select(t => new TopExpenseDto(t.Id, t.Date, t.Description, t.Category.Name, t.Amount))
            .ToListAsync(ct);

        return new DashboardCashDto(topExpenses, await PendingRecurring(today, ct), monthsWithoutBook, negativeMonths);
    }

    // Despesas fixas que valem para o MÊS ATUAL (independe do período escolhido) e ainda não foram lançadas.
    // Mesma regra do "aplicar despesas fixas" do livro: reaproveita o CashBookRules.RecurringToApply.
    private async Task<List<PendingRecurringDto>> PendingRecurring(DateOnly today, CancellationToken ct)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var applied = (await db.CashTransactions.AsNoTracking()
            .Where(t => t.RecurringExpenseId != null && t.Date >= monthStart && t.Date < monthEnd)
            .Select(t => t.RecurringExpenseId!.Value)
            .ToListAsync(ct)).ToHashSet();

        var active = await db.RecurringExpenses.AsNoTracking()
            .Where(e => e.IsActive)
            .ToListAsync(ct);

        return CashBookRules.RecurringToApply(active, today.Year, today.Month, applied).ToCreate
            .OrderBy(e => e.DayOfMonth)
            .Select(e => new PendingRecurringDto(e.Id, e.Description, e.Amount))
            .ToList();
    }

    // Busca UMA vez o que o período tem (estadias, caixa, bloqueios) e fatia em memória.
    // Chamar o Metrics para cada dia seriam 6 consultas × 31 dias = 186 idas ao banco.
    private async Task<List<SeriesPointDto>> Series(DashboardPeriod period, CancellationToken ct)
    {
        var (from, to) = (period.From, period.To);
        var end = to.AddDays(1);

        var stays = await db.Reservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Confirmed && r.CheckIn < end && from < r.CheckOut)
            .Select(r => new StayTotal(r.CheckIn, r.CheckOut, r.Total))
            .ToListAsync(ct);

        var cash = await db.CashTransactions.AsNoTracking()
            .Where(t => t.Date >= from && t.Date <= to)
            .Select(t => new CashEntry(t.Date, t.Type, t.Amount))
            .ToListAsync(ct);

        var blocked = (await db.BlockedDates.AsNoTracking()
            .Where(b => b.Date >= from && b.Date <= to)
            .Select(b => b.Date)
            .ToListAsync(ct)).ToHashSet();

        return DashboardRules.Series(DashboardRules.Buckets(period), stays, cash, blocked)
            .Select(p => new SeriesPointDto(p.Bucket.Label, p.Bucket.From, p.Bucket.To,
                p.ReservationRevenue, p.CashIncome, p.CashExpense, p.CashNet, p.OccupancyRate))
            .ToList();
    }

    // Agrupa no banco (GROUP BY + SUM): volta uma linha por categoria, não as transações.
    private Task<List<ExpenseByCategoryDto>> ExpensesByCategory(DateOnly from, DateOnly to, CancellationToken ct) =>
        db.CashTransactions.AsNoTracking()
            .Where(t => t.Type == CashTransactionType.Expense && t.Date >= from && t.Date <= to)
            .GroupBy(t => new { t.CategoryId, t.Category.Name, t.Category.Color })
            .OrderByDescending(g => g.Sum(t => t.Amount))
            .Select(g => new ExpenseByCategoryDto(g.Key.CategoryId, g.Key.Name, g.Key.Color, g.Sum(t => t.Amount)))
            .ToListAsync(ct);

    // Kits das reservas confirmadas com check-in no período, pelo SNAPSHOT (nome e preço da época da reserva).
    private Task<List<TopKitDto>> TopKits(DateOnly from, DateOnly to, CancellationToken ct) =>
        db.Reservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Confirmed && r.CheckIn >= from && r.CheckIn <= to
                        && r.KitName != null)
            .GroupBy(r => r.KitName!)
            .OrderByDescending(g => g.Count()).ThenByDescending(g => g.Sum(r => r.KitPrice ?? 0))
            .Select(g => new TopKitDto(g.Key, g.Count(), g.Sum(r => r.KitPrice ?? 0)))
            .ToListAsync(ct);

    // Reservas confirmadas não se sobrepõem, então os eventos delas saem em sequência:
    // as 10 primeiras estadias (por check-in) que ainda não acabaram já contêm os 10 próximos eventos.
    private async Task<List<UpcomingDto>> Upcoming(CancellationToken ct)
    {
        var today = clock.Today();

        var stays = await db.Reservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Confirmed && r.CheckOut >= today)
            .OrderBy(r => r.CheckIn)
            .Take(UpcomingCount)
            .Select(r => new AgendaStay(r.Id, r.Guest1.Name, r.CheckIn, r.CheckOut))
            .ToListAsync(ct);

        return DashboardRules.Agenda(stays, today, UpcomingCount)
            .Select(e => new UpcomingDto(e.ReservationId, e.GuestName, e.Date, e.Type))
            .ToList();
    }
}
