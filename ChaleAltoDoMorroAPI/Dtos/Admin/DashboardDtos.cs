using System.ComponentModel.DataAnnotations;
using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Dtos.Admin;

// GET /api/admin/dashboard?period=quarter&year=2026&index=4. O index é ignorado no "year".
public class DashboardQuery : IValidatableObject
{
    [Required] public PeriodKind? Period { get; set; }
    [Required] public int? Year { get; set; }
    public int? Index { get; set; }

    // Só chamar depois da validação: com a query inválida o [ApiController] devolve 400 antes do controller rodar.
    public DashboardPeriod ToPeriod() => DashboardPeriod.Of(Period!.Value, Year!.Value, Index ?? 0)!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Period is not { } kind || Year is not { } year)
            yield break; // o [Required] já reclamou

        if (!Enum.IsDefined(kind))
            yield return new ValidationResult("Use month, quarter, semester ou year.", [nameof(Period)]);
        else if (year is < 2000 or > 2100)
            yield return new ValidationResult("O ano deve estar entre 2000 e 2100.", [nameof(Year)]);
        else if (DashboardPeriod.Of(kind, year, Index ?? 0) is null)
            yield return new ValidationResult(IndexMessage(kind), [nameof(Index)]);
    }

    private static string IndexMessage(PeriodKind kind) => kind switch
    {
        PeriodKind.Month => "Para mês, o index vai de 1 a 12.",
        PeriodKind.Quarter => "Para trimestre, o index vai de 1 a 4.",
        _ => "Para semestre, o index vai de 1 a 2."
    };
}

// GET /api/admin/dashboard — campos na ordem do CONTRATO_PORTAL.md, seção "Dashboard".
public record DashboardDto(
    PeriodDto Period, DashboardMetricsDto Current, DashboardMetricsDto Previous,
    IReadOnlyList<SeriesPointDto> Series, IReadOnlyList<ExpenseByCategoryDto> ExpensesByCategory,
    IReadOnlyList<TopKitDto> TopKits, DashboardCashDto Cash, IReadOnlyList<UpcomingDto> Upcoming, int PendingCount);

public record SeriesPointDto(
    string Label, DateOnly From, DateOnly To, decimal ReservationRevenue,
    decimal CashIncome, decimal CashExpense, decimal CashNet, decimal OccupancyRate);

public record ExpenseByCategoryDto(int CategoryId, string Name, string Color, decimal Total);

public record TopKitDto(string Name, int Count, decimal Revenue);

public record UpcomingDto(Guid ReservationId, string GuestName, DateOnly Date, AgendaType Type);

public record PeriodDto(PeriodKind Kind, int Year, int Index, DateOnly From, DateOnly To, string Label);

// Usado duas vezes: no "current" e no "previous".
public record DashboardMetricsDto(
    decimal ReservationRevenue, int ConfirmedReservations, int NightsSold, int AvailableNights,
    decimal OccupancyRate, decimal AverageTicket, decimal CashIncome, decimal CashExpense, decimal CashNet);

public record DashboardCashDto(
    IReadOnlyList<TopExpenseDto> TopExpenses,
    IReadOnlyList<PendingRecurringDto> PendingRecurring,
    IReadOnlyList<string> MonthsWithoutBook,
    IReadOnlyList<string> NegativeMonths);

public record TopExpenseDto(Guid Id, DateOnly Date, string Description, string CategoryName, decimal Amount);

public record PendingRecurringDto(int Id, string Description, decimal Amount);
