namespace ChaleAltoDoMorroAPI.Domain;

// Regras do Livro Caixa PURAS: sem EF, sem HTTP, sem relógio ("agora" chega por parâmetro).
// Casos de teste em docs/REGRAS_CAIXA.md.
public static class CashBookRules
{
    public const int MinYear = 2020;
    public const int MaxYear = 2100;
    public const decimal MaxAmount = 9_999_999.99m;

    /// <summary>Regra 2: mês de 1 a 12, ano de 2020 a 2100.</summary>
    public static bool IsValidPeriod(int year, int month) =>
        year is >= MinYear and <= MaxYear && month is >= 1 and <= 12;

    /// <summary>Regra 8: positivo, no máximo 2 casas e até 9.999.999,99.</summary>
    public static bool IsValidAmount(decimal amount) =>
        amount > 0 && amount <= MaxAmount && amount == Math.Round(amount, 2);

    /// <summary>
    /// Devolve o primeiro problema da transação, ou null se ela pode ser gravada.
    /// <paramref name="currentCategoryId"/>: null ao criar; na edição, a categoria que a transação já tinha.
    /// </summary>
    public static CashError? ValidateTransaction(CashBook book, DateOnly date, CashTransactionType type,
        decimal amount, CashCategory category, int? currentCategoryId = null)
    {
        if (book.Status == CashBookStatus.Closed)
            return new BookClosed();

        if (!IsValidAmount(amount))
            return new InvalidAmount();

        // Regras 7 e 11: a data fica no mês do livro (mudar de mês = apagar e lançar no outro livro).
        if (date.Year != book.Year || date.Month != book.Month)
            return new DateOutsideBook(book.Year, book.Month);

        if (category.Type != type)
            return new CategoryTypeMismatch();

        // Regra 10: desativada só vale para quem já estava nela antes de ser desativada.
        if (!category.IsActive && category.Id != currentCategoryId)
            return new CategoryInactive();

        return null;
    }

    /// <summary>Regra 25: totais do livro. Soma em memória: lista vazia dá 0, nunca null.</summary>
    public static CashSummary Summarize(IEnumerable<CashTransaction> transactions)
    {
        var list = transactions.ToList();

        var income = SumOf(list, CashTransactionType.Income);
        var expense = SumOf(list, CashTransactionType.Expense);

        var byCategory = list
            .GroupBy(t => new { t.CategoryId, t.Type })
            .Select(g => new CategoryTotal(g.Key.CategoryId, g.Key.Type, g.Sum(t => t.Amount)))
            .OrderBy(c => c.Type)
            .ThenByDescending(c => c.Total)
            .ToList();

        var daily = list
            .GroupBy(t => t.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyTotal(g.Key, SumOf(g, CashTransactionType.Income), SumOf(g, CashTransactionType.Expense)))
            .ToList();

        return new CashSummary(income, expense, income - expense, byCategory, daily);
    }

    /// <summary>Regra 12: dia DayOfMonth do mês; se o mês for mais curto, o último dia (31 em fevereiro → 28/29).</summary>
    public static DateOnly RecurringDate(int dayOfMonth, int year, int month)
    {
        var day = Math.Min(dayOfMonth, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, day);
    }

    /// <summary>Regra 12: ativa, já começou e ainda não terminou no mês.</summary>
    public static bool IsDue(RecurringExpense expense, int year, int month)
    {
        var target = new DateOnly(year, month, 1);

        return expense.IsActive
            && expense.StartMonth <= target
            && (expense.EndMonth is null || expense.EndMonth >= target);
    }

    /// <summary>Regras 12 e 13: o que criar no mês, sem repetir as que já têm transação no livro.</summary>
    public static RecurringPlan RecurringToApply(IEnumerable<RecurringExpense> models, int year, int month,
        IReadOnlySet<int> alreadyAppliedIds)
    {
        var due = models.Where(m => IsDue(m, year, month)).ToList();
        var toCreate = due.Where(m => !alreadyAppliedIds.Contains(m.Id)).ToList();

        return new RecurringPlan(toCreate, due.Count - toCreate.Count);
    }

    /// <summary>Regras 12 e 14: a transação nasce como cópia do modelo e depois é independente dele.</summary>
    public static CashTransaction FromRecurring(RecurringExpense expense, CashBook book, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CashBookId = book.Id,
        Date = RecurringDate(expense.DayOfMonth, book.Year, book.Month),
        Type = CashTransactionType.Expense,
        Amount = expense.Amount,
        Description = expense.Description,
        CategoryId = expense.CategoryId,
        PaymentMethod = expense.PaymentMethod,
        RecurringExpenseId = expense.Id,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static decimal SumOf(IEnumerable<CashTransaction> transactions, CashTransactionType type) =>
        transactions.Where(t => t.Type == type).Sum(t => t.Amount);


}
