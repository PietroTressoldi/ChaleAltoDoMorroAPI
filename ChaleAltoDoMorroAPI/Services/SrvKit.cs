using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

public enum KitDeleteOutcome { NotFound, Deleted, Deactivated }

public class SrvKit(AppDbContext db)
{
    // Admin vê todos (inclusive inativos); o site só vê os ativos (/api/extras).
    public async Task<List<AdminKitDto>> GetAll(CancellationToken ct = default) =>
        await db.Kits
            .AsNoTracking()
            .OrderBy(k => k.Order).ThenBy(k => k.Name)
            .Select(k => new AdminKitDto(k.Id, k.Name, k.Description, k.Price, k.Items, k.IsActive, k.Order))
            .ToListAsync(ct);

    public async Task<AdminResult<AdminKitDto>> Get(Guid id, CancellationToken ct = default)
    {
        var kit = await db.Kits.AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, ct);
        return kit is null ? NotFoundResult() : new Success<AdminKitDto>(ToDto(kit));
    }

    public async Task<AdminKitDto> Create(SaveKitRequest request, CancellationToken ct = default)
    {
        var kit = new Kit();
        Apply(kit, request);
        db.Kits.Add(kit);
        await db.SaveChangesAsync(ct);
        return ToDto(kit);
    }

    public async Task<AdminResult<AdminKitDto>> Update(Guid id, SaveKitRequest request, CancellationToken ct = default)
    {
        var kit = await db.Kits.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kit is null)
            return NotFoundResult();

        Apply(kit, request);
        await db.SaveChangesAsync(ct);
        return new Success<AdminKitDto>(ToDto(kit));
    }

    // PATCH: muda só o IsActive, sem precisar reenviar o kit inteiro.
    public async Task<AdminResult<AdminKitDto>> SetActive(Guid id, bool isActive, CancellationToken ct = default)
    {
        var kit = await db.Kits.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kit is null)
            return NotFoundResult();

        kit.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        return new Success<AdminKitDto>(ToDto(kit));
    }

    // Kit que já aparece em alguma reserva não é apagado, só desativado: o histórico continua apontando para ele.
    public async Task<KitDeleteOutcome> Delete(Guid id, CancellationToken ct = default)
    {
        var kit = await db.Kits.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (kit is null)
            return KitDeleteOutcome.NotFound;

        if (await db.Reservations.AnyAsync(r => r.KitId == id, ct))
        {
            kit.IsActive = false;
            await db.SaveChangesAsync(ct);
            return KitDeleteOutcome.Deactivated;
        }

        db.Kits.Remove(kit);
        await db.SaveChangesAsync(ct);
        return KitDeleteOutcome.Deleted;
    }

    private static void Apply(Kit kit, SaveKitRequest request)
    {
        kit.Name = request.Name;
        kit.Description = request.Description;
        kit.Price = request.Price;
        kit.Items = request.Items;
        kit.IsActive = request.IsActive;
        kit.Order = request.Order;
    }

    private static AdminKitDto ToDto(Kit k) => new(k.Id, k.Name, k.Description, k.Price, k.Items, k.IsActive, k.Order);

    private static NotFound<AdminKitDto> NotFoundResult() => new("Kit não encontrado.");
}
