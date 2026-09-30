using System.Globalization;
using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Tests.Fixtures;

// Cenário de referência de docs/REGRAS_CAIXA.md: livro de outubro/2026, aberto, com 5 lançamentos.
// Tudo é método (não campo): cada teste recebe objetos NOVOS, então fechar o livro num teste não afeta o outro.
public static class CashScenario
{
    public static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.FromHours(-3));

    public static CashCategory Hospedagem() => Category(1, "Hospedagem", CashTransactionType.Income);
    public static CashCategory Kits() => Category(2, "Kits e extras", CashTransactionType.Income);
    public static CashCategory Limpeza() => Category(4, "Limpeza", CashTransactionType.Expense);
    public static CashCategory Energia() => Category(6, "Energia", CashTransactionType.Expense);
    public static CashCategory Internet() => Category(8, "Internet", CashTransactionType.Expense);

    public static CashBook October(CashBookStatus status = CashBookStatus.Open) =>
        new() { Id = 10, Year = 2026, Month = 10, Status = status };

    public static List<CashTransaction> ReferenceTransactions() =>
    [
        Transaction("2026-10-05", Energia(), 250m, "Conta de luz"),
        Transaction("2026-10-09", Hospedagem(), 1789m, "Reserva — Ana Souza (09/10–11/10)"),
        Transaction("2026-10-09", Kits(), 179m, "Kit Toque de Romance"),
        Transaction("2026-10-10", Internet(), 120m, "Internet"),
        Transaction("2026-10-12", Limpeza(), 300m, "Faxina pós-estadia"),
    ];

    public static CashTransaction Transaction(string date, CashCategory category, decimal amount, string description = "Lançamento") => new()
    {
        Id = Guid.NewGuid(),
        CashBookId = 10,
        Date = D(date),
        Type = category.Type,
        Amount = amount,
        Description = description,
        CategoryId = category.Id,
        Category = category,
        PaymentMethod = PaymentMethod.Pix
    };

    // Luz (desde 2026-10) · Internet (desde 2026-11) · Seguro (2026-01 a 2026-09) · Jardinagem (inativa).
    public static List<RecurringExpense> RecurringModels() =>
    [
        Recurring(1, "Luz", 250m, 6, 10, "2026-10-01", null),
        Recurring(2, "Internet", 120m, 8, 15, "2026-11-01", null),
        Recurring(3, "Seguro", 90m, 6, 20, "2026-01-01", "2026-09-01"),
        Recurring(4, "Jardinagem", 150m, 6, 5, "2026-01-01", null, isActive: false),
    ];

    public static DateOnly D(string date) => DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static RecurringExpense Recurring(int id, string description, decimal amount, int categoryId, int day,
        string start, string? end, bool isActive = true) => new()
    {
        Id = id,
        Description = description,
        Amount = amount,
        CategoryId = categoryId,
        DayOfMonth = day,
        PaymentMethod = PaymentMethod.Pix,
        IsActive = isActive,
        StartMonth = D(start),
        EndMonth = end is null ? null : D(end)
    };

    private static CashCategory Category(int id, string name, CashTransactionType type) =>
        new() { Id = id, Name = name, Type = type, Color = "#000000", Icon = "circle", IsActive = true };
}
