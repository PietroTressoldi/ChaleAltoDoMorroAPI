using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// Única fonte da verdade da disponibilidade: bloqueios manuais + noites das reservas confirmadas.
// As noites das reservas são calculadas na hora (nada é gravado em BlockedDate), então cancelar libera sozinho.
public class SrvAvailability(AppDbContext db, IClock brazilClock)
{
    /// <summary>Noites indisponíveis de hoje em diante, sem repetição e em ordem de data.</summary>
    public async Task<List<DateOnly>> GetUnavailableNights(CancellationToken ct = default)
    {
        var today = brazilClock.Today();

        var blockedDates = await db.BlockedDates
            .AsNoTracking()
            .Where(b => b.Date >= today)
            .Select(b => b.Date)
            .ToListAsync(ct);

        // > today: reserva com check-out hoje não ocupa mais nenhuma noite.
        var confirmedStays = await db.Reservations
            .Where(r => r.Status == ReservationStatus.Confirmed && r.CheckOut > today)
            .Select(r => new { r.CheckIn, r.CheckOut })
            .ToListAsync(ct);

        // Mesma expansão [checkIn, checkOut) da validação, testada em BookingRulesNightsTests.
        // O filtro tira as noites já passadas de uma reserva em andamento.
        var reservedNights = confirmedStays
            .SelectMany(s => BookingRules.Nights(s.CheckIn, s.CheckOut))
            .Where(n => n >= today);

        return blockedDates
            .Concat(reservedNights)
            .Distinct()
            .Order()
            .ToList();
    }
}
