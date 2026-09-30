namespace ChaleAltoDoMorroAPI.Domain;

// Resultado do resumo de um livro (regra 25). Nome e cor da categoria não estão aqui:
// quem busca isso no banco é o serviço, que junta os dois para montar o DTO.
public record CashSummary(
    decimal Income,
    decimal Expense,
    decimal Net,
    IReadOnlyList<CategoryTotal> ByCategory,
    IReadOnlyList<DailyTotal> Daily);


public record CategoryTotal(int CategoryId, CashTransactionType Type, decimal Total);

public record DailyTotal(DateOnly Date, decimal Income, decimal Expense);

// O que "aplicar despesas fixas" deve criar, e quantas já estavam lançadas (regra 13: created/skipped).
public record RecurringPlan(IReadOnlyList<RecurringExpense> ToCreate, int Skipped);
