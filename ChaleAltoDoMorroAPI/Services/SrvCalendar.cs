using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// Calendário do portal (task 18): o que ocupa ou marca noites no intervalo, com os detalhes que o site não vê.
public class SrvCalendar(AppDbContext db)
{

    public async Task<CalendarDto> Get(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        // Converte o "to" inclusivo em fim EXCLUSIVO, igual às noites [checkIn, checkOut).
        // Assim "tocar o intervalo" vira a mesma fórmula do PackageRules.Overlaps: início < fimDoOutro && inícioDoOutro < fim.
        var end = to.AddDays(1);

        var reservations = await db.Reservations.AsNoTracking()
            .Where(r => (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed)
                        && r.CheckIn < end && from < r.CheckOut)
            .OrderBy(r => r.CheckIn)
            .Select(r => new CalendarReservationDto(
                r.Id, r.Status, r.CheckIn, r.CheckOut, r.Guest1.Name, r.Total, r.KitName, r.PackageName,
                r.BufferBeforeNights ?? 0, r.BufferAfterNights ?? 0))
            .ToListAsync(ct);

        var blocks = await db.BlockedDates.AsNoTracking()
            .Where(b => b.Date >= from && b.Date <= to)
            .OrderBy(b => b.Date)
            .Select(b => new CalendarBlockDto(b.Date, b.Reason))
            .ToListAsync(ct);

        var packages = await db.SpecialPackages.AsNoTracking()
            .Where(p => p.CheckIn < end && from < p.CheckOut)
            .OrderBy(p => p.CheckIn)
            .Select(p => new CalendarPackageDto(p.Id, p.Name, p.CheckIn, p.CheckOut, p.Price))
            .ToListAsync(ct);

        return new CalendarDto(reservations, blocks, packages, await BufferNights(from, to, end, ct));
    }

    // A folga pode cair no intervalo sem a reserva estar nele (ex.: 28/09 → 01/10 com folga depois = 1 marca o dia 01/10).
    // Por isso busca as confirmadas numa janela alargada pela folga máxima e depois recorta as datas ao intervalo.
    private async Task<List<CalendarBufferNightDto>> BufferNights(DateOnly from, DateOnly to, DateOnly end, CancellationToken ct)
    {
        var searchFrom = from.AddDays(-BookingSettings.MaxBufferNights);
        var searchEnd = end.AddDays(BookingSettings.MaxBufferNights);

        var confirmed = await db.Reservations.AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Confirmed && r.CheckIn < searchEnd && searchFrom < r.CheckOut)
            .Select(r => new { r.Id, r.CheckIn, r.CheckOut, r.BufferBeforeNights, r.BufferAfterNights })
            .ToListAsync(ct);

        return confirmed
            .SelectMany(r => OccupancyRules
                .BufferNights(new BufferedStay(r.CheckIn, r.CheckOut, r.BufferBeforeNights ?? 0, r.BufferAfterNights ?? 0))
                .Where(date => date >= from && date <= to)
                .Select(date => new CalendarBufferNightDto(date, r.Id)))
            .OrderBy(b => b.Date)
            .ToList();
    }
}