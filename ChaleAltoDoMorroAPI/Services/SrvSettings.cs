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
        var settings = await GetTrackedSettings(ct);

        settings.BufferBeforeNights = request.BufferBeforeNights;
        settings.BufferAfterNights = request.BufferAfterNights;
        await db.SaveChangesAsync(ct);

        return new BufferSettingsDto(settings.BufferBeforeNights, settings.BufferAfterNights);
    }

    // ---------- Horários (task 24) ----------

    public async Task<ScheduleSettingsDto> GetSchedule(CancellationToken ct = default) =>
        ToScheduleDto(await GetBookingSettings(ct));

    public async Task<ScheduleSettingsDto> PutSchedule(ScheduleSettingsDto request, CancellationToken ct = default)
    {
        var settings = await GetTrackedSettings(ct);

        settings.CheckInTime = request.CheckInTime;
        settings.CheckOutTime = request.CheckOutTime;
        settings.ExtendedCheckInTime = request.ExtendedCheckInTime;
        settings.ExtendedCheckOutTime = request.ExtendedCheckOutTime;
        await db.SaveChangesAsync(ct);

        return ToScheduleDto(settings);
    }

    // ---------- Meses abertos (task 28) ----------

    public async Task<BookingWindowDto> GetBookingWindow(CancellationToken ct = default) =>
        new((await GetBookingSettings(ct)).BookingOpenUntil);

    // Qualquer dia do mês serve: grava o último dia dele. Normaliza aqui em vez de confiar no que o portal manda.
    public async Task<BookingWindowDto> PutBookingWindow(BookingWindowDto request, CancellationToken ct = default)
    {
        var settings = await GetTrackedSettings(ct);
        settings.BookingOpenUntil = request.OpenUntil is { } date ? BookingRules.EndOfMonth(date) : null;
        await db.SaveChangesAsync(ct);

        return new BookingWindowDto(settings.BookingOpenUntil);
    }


    // Versão RASTREADA (sem AsNoTracking), para os PUTs alterarem e salvarem.
    private async Task<BookingSettings> GetTrackedSettings(CancellationToken ct)
    {
        var settings = await db.BookingSettings.FirstOrDefaultAsync(s => s.Id == BookingSettings.SingletonId, ct);
        if (settings is null)
        {
            settings = new BookingSettings();
            db.BookingSettings.Add(settings);
        }

        return settings;
    }

    public async Task<PublicBookingSettingsDto> GetPublicBookingSettings(CancellationToken ct = default)
    {
        var settings = await GetBookingSettings(ct);
        return new PublicBookingSettingsDto(settings.BookingOpenUntil, settings.BufferBeforeNights, settings.BufferAfterNights);
    }

    private static ScheduleSettingsDto ToScheduleDto(BookingSettings s) =>
        new(s.CheckInTime, s.CheckOutTime, s.ExtendedCheckInTime, s.ExtendedCheckOutTime);
}