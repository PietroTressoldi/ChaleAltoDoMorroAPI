using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ChaleAltoDoMorroAPI.Services;

public class SrvCashBook(AppDbContext db, TimeProvider timeProvider)
{
    private static readonly string[] MonthNames =
        ["janeiro", "fevereiro", "março", "abril", "maio", "junho",
         "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    // ---------- Leitura ----------

    public async Task<List<CashBookSummaryDto>> List(CashBookQuery query, CancellationToken ct = default)
    {
        var books = db.CashBooks.AsNoTracking();

        if (query.Year is not null)
            books = books.Where(b => b.Year == query.Year);

        if (query.Status is not null)
            books = books.Where(b => b.Status == query.Status);

        var summaries = await ToSummaries(books).ToListAsync(ct);

        // Ordena em memória: são no máximo 12 livros por ano, e os totais só existem depois da projeção.
        Func<CashBookSummaryDto, decimal> key = (query.Sort ?? CashBookSort.Period) switch
        {
            CashBookSort.Income => s => s.Income,
            CashBookSort.Expense => s => s.Expense,
            CashBookSort.Net => s => s.Net,
            _ => s => s.Year * 100 + s.Month
        };

        var ordered = (query.Order ?? SortOrder.Desc) == SortOrder.Desc
            ? summaries.OrderByDescending(key)
            : summaries.OrderBy(key);

        return ordered.ThenByDescending(s => s.Year * 100 + s.Month).ToList();
    }

    // Regra 26: sempre 12 meses; mês sem livro vem com hasBook = false e zeros.
    public async Task<CashYearSummaryDto> YearSummary(int year, CancellationToken ct = default)
    {
        var summaries = await ToSummaries(db.CashBooks.AsNoTracking().Where(b => b.Year == year)).ToListAsync(ct);
        var byMonth = summaries.ToDictionary(s => s.Month);

        var months = Enumerable.Range(1, 12)
            .Select(month => byMonth.TryGetValue(month, out var s)
                ? new CashYearMonthDto(month, true, s.Status, s.Income, s.Expense, s.Net)
                : new CashYearMonthDto(month, false, null, 0m, 0m, 0m))
            .ToList();

        var income = months.Sum(m => m.Income);
        var expense = months.Sum(m => m.Expense);
        return new CashYearSummaryDto(year, income, expense, income - expense, months);
    }

    public async Task<AdminResult<CashBookDetailDto>> GetByPeriod(int year, int month, CancellationToken ct = default)
    {
        var book = await db.CashBooks.AsNoTracking().FirstOrDefaultAsync(b => b.Year == year && b.Month == month, ct);

        return book is null
            ? new NotFound<CashBookDetailDto>($"Ainda não existe livro de {Period(year, month)}.")
            : new Success<CashBookDetailDto>(await Detail(book, ct));
    }

    // ---------- Escrita ----------

    public async Task<AdminResult<CashBookDetailDto>> Create(CreateCashBookRequest request, CancellationToken ct = default)
    {
        if (await db.CashBooks.AnyAsync(b => b.Year == request.Year && b.Month == request.Month, ct))
            return DuplicateBook(request.Year, request.Month);

        var now = timeProvider.GetUtcNow();
        var book = new CashBook
        {
            Year = request.Year,
            Month = request.Month,
            Status = CashBookStatus.Open,
            Notes = request.Notes?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.CashBooks.Add(book);

        // Regra 3: o livro já nasce com as despesas fixas do mês. Livro novo = nenhuma lançada ainda.
        if (request.ApplyRecurring)
        {
            var plan = CashBookRules.RecurringToApply(await ActiveRecurring(ct), book.Year, book.Month, new HashSet<int>());
            foreach (var expense in plan.ToCreate)
                book.Transactions.Add(CashBookRules.FromRecurring(expense, book, now));
        }

        // Um SaveChanges só = uma transação no banco: ou grava o livro E as despesas, ou nada.
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Duas abas criando o mesmo mês ao mesmo tempo: as duas passaram no AnyAsync, o índice único barrou a segunda.
            return DuplicateBook(request.Year, request.Month);
        }

        return new Success<CashBookDetailDto>(await Detail(book, ct));
    }

    // Notas podem mudar mesmo com o livro fechado: o "somente leitura" da regra 4 vale para os lançamentos.
    public async Task<AdminResult<CashBookSummaryDto>> UpdateNotes(int id, UpdateCashBookNotesRequest request, CancellationToken ct = default)
    {
        var book = await db.CashBooks.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null)
            return NotFoundResult<CashBookSummaryDto>();

        book.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        book.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return new Success<CashBookSummaryDto>(await Summary(id, ct));
    }

    public async Task<AdminResult<CashBookSummaryDto>> Close(int id, CancellationToken ct = default)
    {
        var book = await db.CashBooks.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null)
            return NotFoundResult<CashBookSummaryDto>();

        if (!book.TryClose(timeProvider.GetUtcNow()))
            return new Conflict<CashBookSummaryDto>($"O livro de {Period(book.Year, book.Month)} já está fechado.");

        await db.SaveChangesAsync(ct);
        return new Success<CashBookSummaryDto>(await Summary(id, ct));
    }

    public async Task<AdminResult<CashBookSummaryDto>> Reopen(int id, CancellationToken ct = default)
    {
        var book = await db.CashBooks.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null)
            return NotFoundResult<CashBookSummaryDto>();

        if (!book.TryReopen(timeProvider.GetUtcNow()))
            return new Conflict<CashBookSummaryDto>($"O livro de {Period(book.Year, book.Month)} já está aberto.");

        await db.SaveChangesAsync(ct);
        return new Success<CashBookSummaryDto>(await Summary(id, ct));
    }

    // Regras 12, 13 e 15: cria só as que ainda não têm transação neste livro. Aplicar de novo não duplica.
    public async Task<AdminResult<ApplyRecurringResponse>> ApplyRecurring(int id, CancellationToken ct = default)
    {
        var book = await db.CashBooks.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null)
            return NotFoundResult<ApplyRecurringResponse>();

        if (book.Status == CashBookStatus.Closed)
            return new Conflict<ApplyRecurringResponse>(ClosedMessage(book));

        var alreadyApplied = await db.CashTransactions
            .Where(t => t.CashBookId == id && t.RecurringExpenseId != null)
            .Select(t => t.RecurringExpenseId!.Value)
            .ToHashSetAsync(ct);

        var now = timeProvider.GetUtcNow();
        var plan = CashBookRules.RecurringToApply(await ActiveRecurring(ct), book.Year, book.Month, alreadyApplied);

        foreach (var expense in plan.ToCreate)
            db.CashTransactions.Add(CashBookRules.FromRecurring(expense, book, now));

        if (plan.ToCreate.Count > 0)
            book.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Dois cliques rápidos: o índice único filtrado barrou a duplicata do segundo.
            return new Conflict<ApplyRecurringResponse>("As despesas fixas já estão sendo aplicadas. Recarregue o livro.");
        }

        return new Success<ApplyRecurringResponse>(new ApplyRecurringResponse(plan.ToCreate.Count, plan.Skipped));
    }

    // Regra 6: só apaga livro vazio. O banco também barra (Restrict), mas aqui a mensagem sai em PT-BR.
    public async Task<AdminResult<bool>> Delete(int id, CancellationToken ct = default)
    {
        var book = await db.CashBooks.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null)
            return NotFoundResult<bool>();

        if (await db.CashTransactions.AnyAsync(t => t.CashBookId == id, ct))
            return new Conflict<bool>($"O livro de {Period(book.Year, book.Month)} tem lançamentos. Apague os lançamentos antes.");

        db.CashBooks.Remove(book);
        await db.SaveChangesAsync(ct);
        return new Success<bool>(true);
    }

    // ---------- Mapeamento (público: a Parte C reaproveita) ----------

    public static CashTransactionDto ToTransactionDto(CashTransaction t) => new(
        t.Id, t.CashBookId, t.Date, t.Type, t.Amount, t.Description,
        t.CategoryId, t.Category.Name, t.Category.Color, t.Category.Icon,
        t.PaymentMethod, t.Notes, t.RecurringExpenseId, t.CreatedAt, t.UpdatedAt);

    public static string ClosedMessage(CashBook book) =>
        $"O livro de {Period(book.Year, book.Month)} está fechado. Reabra para editar.";

    public static string Period(int year, int month) => $"{MonthNames[month - 1]}/{year}";

    // ---------- Internos ----------

    // Totais calculados NO BANCO (uma consulta para a lista toda, sem trazer as transações).
    // (decimal?) + ?? 0: SUM de nenhuma linha no SQL é NULL, não 0 (a armadilha da task 14).
    private static IQueryable<CashBookSummaryDto> ToSummaries(IQueryable<CashBook> books) =>
        books
            .Select(b => new
            {
                Book = b,
                Income = b.Transactions.Where(t => t.Type == CashTransactionType.Income).Sum(t => (decimal?)t.Amount) ?? 0m,
                Expense = b.Transactions.Where(t => t.Type == CashTransactionType.Expense).Sum(t => (decimal?)t.Amount) ?? 0m,
                Count = b.Transactions.Count()
            })
            .Select(x => new CashBookSummaryDto(
                x.Book.Id, x.Book.Year, x.Book.Month, x.Book.Status, x.Book.Notes,
                x.Income, x.Expense, x.Income - x.Expense, x.Count, x.Book.ClosedAt));

    private Task<CashBookSummaryDto> Summary(int id, CancellationToken ct) =>
        ToSummaries(db.CashBooks.AsNoTracking().Where(b => b.Id == id)).FirstAsync(ct);

    // Detalhe: traz as transações do mês e usa o CashBookRules (a mesma regra testada) para os totais.
    private async Task<CashBookDetailDto> Detail(CashBook book, CancellationToken ct)
    {
        var transactions = await db.CashTransactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.CashBookId == book.Id)
            .OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        var summary = CashBookRules.Summarize(transactions);
        var categories = transactions.Select(t => t.Category).DistinctBy(c => c.Id).ToDictionary(c => c.Id);

        var byCategory = summary.ByCategory
            .Select(c => new CashCategoryTotalDto(c.CategoryId, categories[c.CategoryId].Name, c.Type, categories[c.CategoryId].Color, c.Total))
            .ToList();

        var daily = summary.Daily.Select(d => new CashDailyDto(d.Date, d.Income, d.Expense)).ToList();

        return new CashBookDetailDto(
            book.Id, book.Year, book.Month, book.Status, book.Notes,
            summary.Income, summary.Expense, summary.Net, transactions.Count, book.ClosedAt,
            transactions.Select(ToTransactionDto).ToList(), byCategory, daily);
    }

    private Task<List<RecurringExpense>> ActiveRecurring(CancellationToken ct) =>
        db.RecurringExpenses.AsNoTracking().Where(e => e.IsActive).ToListAsync(ct);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static Conflict<CashBookDetailDto> DuplicateBook(int year, int month) =>
        new($"Já existe o livro de {Period(year, month)}.");

    private static NotFound<T> NotFoundResult<T>() => new("Livro não encontrado.");
}