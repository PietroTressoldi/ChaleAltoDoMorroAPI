using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;


public enum CashCategoryDeleteOutcome { NotFound, Deleted, Deactivated }

public class SrvCashCategory(AppDbContext db)
{

    public async Task<List<CashCategoryDto>> GetAll(CashTransactionType? type, bool includeInactive, CancellationToken ct = default)
    {
        var query = db.CashCategories.AsNoTracking();

        if (type is not null)
            query = query.Where(c => c.Type == type);

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query
            .OrderBy(c => c.Type).ThenBy(c => c.Order).ThenBy(c => c.Name)
            .Select(c => new CashCategoryDto(c.Id, c.Name, c.Type, c.Color, c.Icon, c.IsActive, c.Order))
            .ToListAsync(ct);
    }

    public async Task<AdminResult<CashCategoryDto>> Get(int id, CancellationToken ct = default)
    {
        var category = await db.CashCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return category is null ? NotFoundResult() : new Success<CashCategoryDto>(ToDto(category));
    }

    public async Task<AdminResult<CashCategoryDto>> Create(SaveCashCategoryRequest request, CancellationToken ct = default)
    {
        if (await NameTaken(request.Name, request.Type!.Value, exceptId: 0, ct))
            return DuplicateName(request);

        var category = new CashCategory();
        Apply(category, request);
        db.CashCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return new Success<CashCategoryDto>(ToDto(category));
    }

    public async Task<AdminResult<CashCategoryDto>> Update(int id, SaveCashCategoryRequest request, CancellationToken ct = default)
    {
        var category = await db.CashCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return NotFoundResult();

        // Trocar entrada ↔ saída numa categoria com lançamentos deixaria saídas numa categoria de entrada (quebra a regra 10).
        if (request.Type != category.Type && await InUse(id, ct))
            return new Conflict<CashCategoryDto>(
                "Essa categoria já tem lançamentos: não dá para trocar entre entrada e saída. Crie uma categoria nova.");

        if (await NameTaken(request.Name, request.Type!.Value, exceptId: id, ct))
            return DuplicateName(request);

        Apply(category, request);
        await db.SaveChangesAsync(ct);
        return new Success<CashCategoryDto>(ToDto(category));
    }

    // Regra 18: com transações ou despesas fixas, "apagar" só desativa (o histórico continua apontando para ela).
    public async Task<CashCategoryDeleteOutcome> Delete(int id, CancellationToken ct = default)
    {
        var category = await db.CashCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
            return CashCategoryDeleteOutcome.NotFound;

        if (await InUse(id, ct))
        {
            category.IsActive = false;
            await db.SaveChangesAsync(ct);
            return CashCategoryDeleteOutcome.Deactivated;
        }

        db.CashCategories.Remove(category);
        await db.SaveChangesAsync(ct);
        return CashCategoryDeleteOutcome.Deleted;
    }

    private async Task<bool> InUse(int id, CancellationToken ct) =>
        await db.CashTransactions.AnyAsync(t => t.CategoryId == id, ct)
        || await db.RecurringExpenses.AnyAsync(e => e.CategoryId == id, ct);

    // Regra 17: nome único por tipo, sem diferenciar maiúsculas. exceptId = 0 ao criar (ids começam em 1).
    private Task<bool> NameTaken(string name, CashTransactionType type, int exceptId, CancellationToken ct)
    {
        var lower = name.Trim().ToLower();
        return db.CashCategories.AnyAsync(c => c.Type == type && c.Id != exceptId && c.Name.ToLower() == lower, ct);
    }

    private static void Apply(CashCategory category, SaveCashCategoryRequest request)
    {
        category.Name = request.Name.Trim();
        category.Type = request.Type!.Value;
        category.Color = request.Color.ToLowerInvariant();
        category.Icon = request.Icon.Trim();
        category.IsActive = request.IsActive;
        category.Order = request.Order;
    }

    private static CashCategoryDto ToDto(CashCategory c) => new(c.Id, c.Name, c.Type, c.Color, c.Icon, c.IsActive, c.Order);

    private static NotFound<CashCategoryDto> NotFoundResult() => new("Categoria não encontrada.");

    private static Conflict<CashCategoryDto> DuplicateName(SaveCashCategoryRequest request) =>
        new($"Já existe uma categoria de {(request.Type == CashTransactionType.Income ? "entrada" : "saída")} chamada \"{request.Name.Trim()}\".");
}

