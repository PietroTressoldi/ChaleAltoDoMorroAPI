using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

public class SrvSettings(AppDbContext db)
{
    // Público: a Parte C (disponibilidade e confirmação) lê os globais por aqui.
    public async Task<BookingSettings> GetBookingSettings(CancellationToken ct = default) =>
        await db.BookingSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == BookingSettings.SingletonId, ct)
        ?? new BookingSettings(); // a migration cria a linha; o fallback só evita 500 num banco montado à mão

    public async Task<BufferSettingsDto> GetBuffer(CancellationToken ct = default)
    {
        var settings = await GetBookingSettings(ct);
        return new BufferSettingsDto(settings.BufferBeforeNights, settings.BufferAfterNights);
    }

    // Só vale para as PRÓXIMAS confirmações: as reservas já confirmadas guardam o próprio snapshot.
    public async Task<BufferSettingsDto> PutBuffer(BufferSettingsDto request, CancellationToken ct = default)
    {
        var settings = await db.BookingSettings.FirstOrDefaultAsync(s => s.Id == BookingSettings.SingletonId, ct);
        if (settings is null)
        {
            settings = new BookingSettings();
            db.BookingSettings.Add(settings);
        }

        settings.BufferBeforeNights = request.BufferBeforeNights;
        settings.BufferAfterNights = request.BufferAfterNights;
        await db.SaveChangesAsync(ct);

        return new BufferSettingsDto(settings.BufferBeforeNights, settings.BufferAfterNights);
    }
}