using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// Única fonte da verdade da disponibilidade: bloqueios manuais + noites das reservas confirmadas.
// As noites das reservas são calculadas na hora (nada é gravado em BlockedDate), então cancelar libera sozinho.
public class SrvAvailability(AppDbContext db, IClock brazilClock)
{
    // Número fixo que identifica o "cadeado do calendário" no Postgres. Qualquer valor serve,
    // desde que todo mundo que mexe na ocupação use o MESMO.
    private const long CalendarLockKey = 20260928;

    /// <summary>
    /// Trava o calendário até o fim da transação atual (pg_advisory_xact_lock). Quem chegar depois ESPERA na fila
    /// e, quando entrar, já enxerga o que o anterior gravou. Chame logo depois de abrir a transação e ANTES de ler
    /// a disponibilidade. Não há "destravar": o Postgres solta sozinho no commit ou no rollback.
    /// </summary>
    public async Task LockCalendar(CancellationToken ct = default)
    {
        // Fora de uma transação, o cadeado seria solto no fim do próprio SELECT, sem proteger nada.
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("LockCalendar precisa de uma transação aberta (db.Database.BeginTransactionAsync).");

        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({CalendarLockKey})", ct);
    }

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
