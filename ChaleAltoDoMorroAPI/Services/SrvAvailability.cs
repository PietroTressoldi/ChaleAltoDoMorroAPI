using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// Única fonte da verdade da disponibilidade: bloqueios manuais + noites das reservas confirmadas (com a folga, task 16).
// As noites das reservas são calculadas na hora (nada é gravado em BlockedDate), então cancelar libera sozinho.
public class SrvAvailability(AppDbContext db, IClock brazilClock, SrvSettings srvSettings)
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
    /// <param name="ignoreReservationId">Reserva que NÃO conta como ocupação: ao editar as datas de uma reserva
    /// confirmada, ela não pode "bater com ela mesma".</param>
    public async Task<List<DateOnly>> GetUnavailableNights(CancellationToken ct = default, Guid? ignoreReservationId = null)
    {
        var today = brazilClock.Today();

        var blockedDates = await db.BlockedDates
            .AsNoTracking()
            .Where(b => b.Date >= today)
            .Select(b => b.Date)
            .ToListAsync(ct);

        var settings = await srvSettings.GetBookingSettings(ct);

        // Quem saiu há até 3 dias ainda pode ter folga depois ocupando hoje (a folga vai no máximo a 3 noites).
        var since = today.AddDays(-BookingSettings.MaxBufferNights);
        var confirmedStays = await ConfirmedStays(r => r.CheckOut > since, ignoreReservationId, ct);

        // Regra 4 (OccupancyRules): noites cobradas + a folga da reserva + a folga que a estadia NOVA vai precisar.
        // Com a folga em 0/0 é a mesma expansão [checkIn, checkOut) de antes. O filtro tira as noites já passadas.
        var reservedNights = confirmedStays
            .SelectMany(s => OccupancyRules.PublishedNights(s, settings.BufferBeforeNights, settings.BufferAfterNights))
            .Where(n => n >= today);

        return blockedDates
            .Concat(reservedNights)
            .Distinct()
            .Order()
            .ToList();
    }

    /// <summary>
    /// O que impede confirmar a estadia com a folga EFETIVA dela (a do pedido ou a global), ou null se pode.
    /// Mais exata que as noites publicadas: usa a regra 3 contra cada reserva confirmada, com o snapshot de cada uma.
    /// Chame depois do LockCalendar.
    /// </summary>
    public async Task<StayError?> ValidateConfirmation(BufferedStay stay, Guid reservationId, CancellationToken ct = default)
    {
        var manualBlocks = (await db.BlockedDates
                .AsNoTracking()
                .Where(b => b.Date >= stay.CheckIn && b.Date < stay.CheckOut)
                .Select(b => b.Date)
                .ToListAsync(ct))
            .ToHashSet();

        // Só as reservas perto o bastante para conflitar: cada folga vai no máximo a 3 noites.
        var from = stay.CheckIn.AddDays(-BookingSettings.MaxBufferNights);
        var to = stay.CheckOut.AddDays(BookingSettings.MaxBufferNights);
        var nearby = await ConfirmedStays(r => r.CheckOut > from && r.CheckIn < to, reservationId, ct);

        return OccupancyRules.ValidateConfirmation(stay, manualBlocks, nearby);
    }

    // Reservas confirmadas com a folga gravada. null = confirmada antes da task 16 → 0/0 (a regra da época).
    private Task<List<BufferedStay>> ConfirmedStays(
        System.Linq.Expressions.Expression<Func<Reservation, bool>> filter, Guid? ignoreReservationId, CancellationToken ct) =>
        db.Reservations
            .AsNoTracking()
            .Where(r => r.Status == ReservationStatus.Confirmed && (ignoreReservationId == null || r.Id != ignoreReservationId))
            .Where(filter)
            .Select(r => new BufferedStay(r.CheckIn, r.CheckOut, r.BufferBeforeNights ?? 0, r.BufferAfterNights ?? 0))
            .ToListAsync(ct);
}
