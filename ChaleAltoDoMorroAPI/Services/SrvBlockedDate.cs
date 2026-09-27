using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// Só os bloqueios MANUAIS. As noites das reservas confirmadas não moram aqui (ver SrvAvailability).
public class SrvBlockedDate(AppDbContext db, IClock brazilClock)
{
    public const int MaxRangeNights = 366;

    public async Task<List<AdminBlockedDateDto>> GetAll(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var start = from ?? brazilClock.Today();

        return await db.BlockedDates
            .AsNoTracking()
            .Where(b => b.Date >= start && (to == null || b.Date <= to))
            .OrderBy(b => b.Date)
            .Select(b => new AdminBlockedDateDto(b.Date, b.Reason))
            .ToListAsync(ct);
    }

    // Uma linha por noite; as que já estão bloqueadas são ignoradas (repetir o pedido não dá erro nem duplica).
    public async Task<AdminResult<BlockedRangeCreatedDto>> CreateRange(CreateBlockedRangeRequest request, CancellationToken ct = default)
    {
        if (request.To < request.From)
            return new Invalid<BlockedRangeCreatedDto>("A data final precisa ser igual ou depois da inicial.");

        if (request.From < brazilClock.Today())
            return new Invalid<BlockedRangeCreatedDto>("Não dá para bloquear noites no passado.");

        // To é inclusivo, então as noites vão até To + 1 (mesma expansão das reservas).
        var nights = BookingRules.Nights(request.From, request.To.AddDays(1));

        if (nights.Count > MaxRangeNights)
            return new Invalid<BlockedRangeCreatedDto>($"Intervalo grande demais (máximo {MaxRangeNights} noites).");

        var existing = await db.BlockedDates
            .Where(b => b.Date >= request.From && b.Date <= request.To)
            .Select(b => b.Date)
            .ToListAsync(ct);

        var newNights = nights.Except(existing).ToList();

        db.BlockedDates.AddRange(newNights.Select(n => new BlockedDate { Date = n, Reason = request.Reason }));
        await db.SaveChangesAsync(ct);

        return new Success<BlockedRangeCreatedDto>(new BlockedRangeCreatedDto(newNights.Count, existing.Count));
    }

    public async Task<bool> Delete(DateOnly date, CancellationToken ct = default) =>
        await db.BlockedDates.Where(b => b.Date == date).ExecuteDeleteAsync(ct) > 0;
}
