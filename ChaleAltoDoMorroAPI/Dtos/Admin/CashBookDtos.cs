using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Validation;

namespace ChaleAltoDoMorroAPI.Dtos.Admin;
// Livro Caixa do portal (task 15). Regras em docs/REGRAS_CAIXA.md.

// Mês no formato "AAAA-MM" (contrato). No banco fica como o dia 1 do mês (2026-10 → 2026-10-01).
public static class MonthText
{
    public const string Pattern = @"^\d{4}-(0[1-9]|1[0-2])$";

    public static DateOnly Parse(string value) =>
        DateOnly.ParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Format(DateOnly month) =>
        month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}

// ---------- Categorias ----------

// Type é nullable + [Required]: se o portal esquecer o campo, vira 400 em vez de cair calado no primeiro valor do enum.
public record SaveCashCategoryRequest(
    [Required, StringLength(60, MinimumLength = 1)] string Name,
    [Required] CashTransactionType? Type,
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "A cor precisa estar no formato #RRGGBB.")] string Color,
    [Required, StringLength(50)] string Icon,
    bool IsActive,
    [Range(0, 1000)] int Order);

public record CashCategoryDto(int Id, string Name, CashTransactionType Type, string Color, string Icon, bool IsActive, int Order);

// ---------- Despesas fixas ----------


public record SaveRecurringExpenseRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Description,
    [Money] decimal Amount,
    int CategoryId,
    [Range(1, 31, ErrorMessage = "O dia do mês vai de 1 a 31.")] int DayOfMonth,
    [Required] PaymentMethod? PaymentMethod,
    bool IsActive,
    [Required, RegularExpression(MonthText.Pattern, ErrorMessage = "Use o formato AAAA-MM.")] string StartMonth,
    [RegularExpression(MonthText.Pattern, ErrorMessage = "Use o formato AAAA-MM.")] string? EndMonth);

public record RecurringExpenseDto(
    int Id,
    string Description,
    decimal Amount,
    int CategoryId,
    string CategoryName,
    int DayOfMonth,
    PaymentMethod PaymentMethod,
    bool IsActive,
    string StartMonth,
    string? EndMonth);

// ---------- Livros ---------

public enum CashBookSort { Period, Income, Expense, Net }

// ?year=2026&status=open&sort=period&order=desc. Sem year, vêm todos os livros.
public class CashBookQuery : IValidatableObject
{
    [Range(2020, 2100)] public int? Year { get; set; }
    public CashBookStatus? Status { get; set; }
    public CashBookSort? Sort { get; set; }
    public SortOrder? Order { get; set; }

    // Mesmo cuidado da ReservationQuery: número passa no binding de enum (sort=7), então conferimos aqui.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status is { } status && !Enum.IsDefined(status))
            yield return new ValidationResult("Status inválido. Use open ou closed.", [nameof(Status)]);
        if (Sort is { } sort && !Enum.IsDefined(sort))
            yield return new ValidationResult("Sort inválido. Use period, income, expense ou net.", [nameof(Sort)]);
        if (Order is { } order && !Enum.IsDefined(order))
            yield return new ValidationResult("Order inválido. Use asc ou desc.", [nameof(Order)]);
    }
}

public record CreateCashBookRequest(
    [Range(2020, 2100, ErrorMessage = "O ano vai de 2020 a 2100.")] int Year,
    [Range(1, 12, ErrorMessage = "O mês vai de 1 a 12.")] int Month,
    [StringLength(1000)] string? Notes,
    bool ApplyRecurring);

public record UpdateCashBookNotesRequest([StringLength(1000)] string? Notes);

public record ApplyRecurringResponse(int Created, int Skipped);

public record CashBookSummaryDto(
    int Id,
    int Year,
    int Month,
    CashBookStatus Status,
    string? Notes,
    decimal Income,
    decimal Expense,
    decimal Net,
    int TransactionCount,
    DateTimeOffset? ClosedAt);

// Contrato: CashBookDetailDto = CashBookSummaryDto + transactions, byCategory e daily (tudo no mesmo nível do JSON).
public record CashBookDetailDto(
    int Id,
    int Year,
    int Month,
    CashBookStatus Status,
    string? Notes,
    decimal Income,
    decimal Expense,
    decimal Net,
    int TransactionCount,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<CashTransactionDto> Transactions,
    IReadOnlyList<CashCategoryTotalDto> ByCategory,
    IReadOnlyList<CashDailyDto> Daily);

public record CashCategoryTotalDto(int CategoryId, string Name, CashTransactionType Type, string Color, decimal Total);

public record CashDailyDto(DateOnly Date, decimal Income, decimal Expense);

public record CashYearSummaryDto(int Year, decimal Income, decimal Expense, decimal Net, IReadOnlyList<CashYearMonthDto> Months);

public record CashYearMonthDto(int Month, bool HasBook, CashBookStatus? Status, decimal Income, decimal Expense, decimal Net);

// ---------- Transações (a Parte C usa o mesmo DTO) ----------

public record CashTransactionDto(
    Guid Id,
    int CashBookId,
    DateOnly Date,
    CashTransactionType Type,
    decimal Amount,
    string Description,
    int CategoryId,
    string CategoryName,
    string CategoryColor,
    string CategoryIcon,
    PaymentMethod PaymentMethod,
    string? Notes,
    int? RecurringExpenseId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Contrato: SaveCashTransactionRequest (mesmo corpo para criar e editar).
public record SaveCashTransactionRequest(
    DateOnly Date,
    [Required] CashTransactionType? Type,
    [Money] decimal Amount,
    [Required, StringLength(200, MinimumLength = 1)] string Description,
    int CategoryId,
    [Required] PaymentMethod? PaymentMethod,
    [StringLength(500)] string? Notes);