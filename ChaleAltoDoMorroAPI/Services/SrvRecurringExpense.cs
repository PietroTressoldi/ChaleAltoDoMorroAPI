using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

public class SrvRecurringExpense(AppDbContext db)
{
    public async Task<List<RecurringExpenseDto>> GetAll(CancellationToken ct = default)
    {
        var rows = await (
                from expense in db.RecurringExpenses.AsNoTracking()
                join category in db.CashCategories on expense.CategoryId equals category.Id
                orderby expense.IsActive descending, expense.DayOfMonth, expense.Description
                select new { expense, CategoryName = category.Name })
            .ToListAsync(ct);

        // O "2026-10" é montado em memória: o EF não traduz DateOnly.ToString("yyyy-MM") para SQL.
        return rows.Select(r => ToDto(r.expense, r.CategoryName)).ToList();
    }

    public async Task<AdminResult<RecurringExpenseDto>> Get(int id, CancellationToken ct = default)
    {
        var row = await (
                from expense in db.RecurringExpenses.AsNoTracking()
                join category in db.CashCategories on expense.CategoryId equals category.Id
                where expense.Id == id
                select new { expense, CategoryName = category.Name })
            .FirstOrDefaultAsync(ct);

        return row is null ? NotFoundResult() : new Success<RecurringExpenseDto>(ToDto(row.expense, row.CategoryName));
    }

    public async Task<AdminResult<RecurringExpenseDto>> Create(SaveRecurringExpenseRequest request, CancellationToken ct = default)
    {
        var category = await FindCategory(request.CategoryId, ct);

        var error = Validate(request, category, currentCategoryId: null);
        if (error is not null)
            return new Invalid<RecurringExpenseDto>(error);

        var expense = new RecurringExpense();
        Apply(expense, request);
        db.RecurringExpenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return new Success<RecurringExpenseDto>(ToDto(expense, category!.Name));
    }

    // Regra 14: editar o modelo NÃO mexe nas transações já lançadas (elas são independentes).
    public async Task<AdminResult<RecurringExpenseDto>> Update(int id, SaveRecurringExpenseRequest request, CancellationToken ct = default)
    {
        var expense = await db.RecurringExpenses.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null)
            return NotFoundResult();

        var category = await FindCategory(request.CategoryId, ct);

        var error = Validate(request, category, currentCategoryId: expense.CategoryId);
        if (error is not null)
            return new Invalid<RecurringExpenseDto>(error);

        Apply(expense, request);
        await db.SaveChangesAsync(ct);
        return new Success<RecurringExpenseDto>(ToDto(expense, category!.Name));
    }

    // Regra 16: as transações já lançadas ficam; o banco zera o recurring_expense_id delas (OnDelete SetNull).
    public async Task<bool> Delete(int id, CancellationToken ct = default)
    {
        var expense = await db.RecurringExpenses.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null)
            return false;

        db.RecurringExpenses.Remove(expense);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private Task<CashCategory?> FindCategory(int id, CancellationToken ct) =>
        db.CashCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    // 422: o formato já passou no DTO; aqui são regras que dependem do banco ou de dois campos juntos.
    private static string? Validate(SaveRecurringExpenseRequest request, CashCategory? category, int? currentCategoryId)
    {
        if (category is null)
            return "Categoria não encontrada.";

        if (category.Type != CashTransactionType.Expense)
            return "Despesa fixa precisa de uma categoria de saída.";

        // Mesma ideia da regra 10: desativada só vale para quem já estava nela.
        if (!category.IsActive && category.Id != currentCategoryId)
            return "Essa categoria está desativada.";

        var start = MonthText.Parse(request.StartMonth);
        DateOnly? end = request.EndMonth is null ? null : MonthText.Parse(request.EndMonth);
        if (end < start)
            return "O mês final não pode ser antes do mês inicial.";

        return null;
    }

    private static void Apply(RecurringExpense expense, SaveRecurringExpenseRequest request)
    {
        expense.Description = request.Description.Trim();
        expense.Amount = request.Amount;
        expense.CategoryId = request.CategoryId;
        expense.DayOfMonth = request.DayOfMonth;
        expense.PaymentMethod = request.PaymentMethod!.Value;
        expense.IsActive = request.IsActive;
        expense.StartMonth = MonthText.Parse(request.StartMonth);
        expense.EndMonth = request.EndMonth is null ? null : MonthText.Parse(request.EndMonth);
    }

    private static RecurringExpenseDto ToDto(RecurringExpense e, string categoryName) => new(
        e.Id, e.Description, e.Amount, e.CategoryId, categoryName, e.DayOfMonth, e.PaymentMethod, e.IsActive,
        MonthText.Format(e.StartMonth), e.EndMonth is null ? null : MonthText.Format(e.EndMonth.Value));

    private static NotFound<RecurringExpenseDto> NotFoundResult() => new("Despesa fixa não encontrada.");
}