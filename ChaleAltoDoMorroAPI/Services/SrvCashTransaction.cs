using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

public class SrvCashTransaction(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<AdminResult<CashTransactionDto>> Create(int cashBookId, SaveCashTransactionRequest request, CancellationToken ct = default)
    {
        var book = await db.CashBooks.FirstOrDefaultAsync(b => b.Id == cashBookId, ct);
        if (book is null)
            return new NotFound<CashTransactionDto>("Livro não encontrado.");

        // Rastreada (sem AsNoTracking): vai virar a navegação Category da transação sem o EF tentar inseri-la de novo.
        var category = await db.CashCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
        if (category is null)
            return new Invalid<CashTransactionDto>("Categoria não encontrada.");

        var error = CashBookRules.ValidateTransaction(book, request.Date, request.Type!.Value, request.Amount, category);
        if (error is not null)
            return ToResult(error, book);

        var now = timeProvider.GetUtcNow();
        var transaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            CashBookId = book.Id,
            CreatedAt = now
        };
        Apply(transaction, request, category, now);
        db.CashTransactions.Add(transaction);
        book.UpdatedAt = now;

        await db.SaveChangesAsync(ct);
        return new Success<CashTransactionDto>(SrvCashBook.ToTransactionDto(transaction));
    }

    public async Task<AdminResult<CashTransactionDto>> Update(Guid id, SaveCashTransactionRequest request, CancellationToken ct = default)
    {
        var transaction = await db.CashTransactions.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (transaction is null)
            return NotFoundResult();

        var book = await db.CashBooks.FirstAsync(b => b.Id == transaction.CashBookId, ct);

        var category = await db.CashCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, ct);
        if (category is null)
            return new Invalid<CashTransactionDto>("Categoria não encontrada.");

        // currentCategoryId: editar mantendo uma categoria que foi desativada depois é permitido (regra 10).
        // A data fora do mês também cobre a regra 11 (mudar de mês = apagar e lançar no outro livro).
        var error = CashBookRules.ValidateTransaction(book, request.Date, request.Type!.Value, request.Amount, category,
            currentCategoryId: transaction.CategoryId);
        if (error is not null)
            return ToResult(error, book);

        var now = timeProvider.GetUtcNow();
        Apply(transaction, request, category, now);
        book.UpdatedAt = now;

        await db.SaveChangesAsync(ct);
        return new Success<CashTransactionDto>(SrvCashBook.ToTransactionDto(transaction));
    }

    // Regra 4: livro fechado é somente leitura, inclusive para apagar.
    public async Task<AdminResult<bool>> Delete(Guid id, CancellationToken ct = default)
    {
        var transaction = await db.CashTransactions.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (transaction is null)
            return new NotFound<bool>("Lançamento não encontrado.");

        var book = await db.CashBooks.FirstAsync(b => b.Id == transaction.CashBookId, ct);
        if (book.Status == CashBookStatus.Closed)
            return new Conflict<bool>(SrvCashBook.ClosedMessage(book));

        db.CashTransactions.Remove(transaction);
        book.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return new Success<bool>(true);
    }

    // Tradução CashError → AdminResult: 409 é ESTADO (livro fechado); 422 é REGRA (data, categoria).
    private static AdminResult<CashTransactionDto> ToResult(CashError error, CashBook book) => error switch
    {
        BookClosed => new Conflict<CashTransactionDto>(SrvCashBook.ClosedMessage(book)),
        DateOutsideBook => new Invalid<CashTransactionDto>(
            $"A data precisa estar dentro de {SrvCashBook.Period(book.Year, book.Month)}. Para outro mês, lance no livro dele."),
        CategoryTypeMismatch => new Invalid<CashTransactionDto>("A categoria não é do mesmo tipo do lançamento (entrada × saída)."),
        CategoryInactive => new Invalid<CashTransactionDto>("Essa categoria está desativada."),
        InvalidAmount => new Invalid<CashTransactionDto>("Valor inválido."), // o [Money] do DTO já barra antes (400)
        _ => throw new InvalidOperationException($"CashError não tratado: {error.GetType().Name}")
    };

    private static void Apply(CashTransaction transaction, SaveCashTransactionRequest request, CashCategory category, DateTimeOffset now)
    {
        transaction.Date = request.Date;
        transaction.Type = request.Type!.Value;
        transaction.Amount = request.Amount;
        transaction.Description = request.Description.Trim();
        transaction.CategoryId = category.Id;
        transaction.Category = category;
        transaction.PaymentMethod = request.PaymentMethod!.Value;
        transaction.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        transaction.UpdatedAt = now;
    }

    private static NotFound<CashTransactionDto> NotFoundResult() => new("Lançamento não encontrado.");
}