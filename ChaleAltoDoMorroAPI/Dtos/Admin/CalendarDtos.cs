using System.ComponentModel.DataAnnotations;
using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Dtos.Admin;

// GET /api/admin/calendar?from=&to= — o "to" é INCLUSIVO (to=2026-10-31 inclui o dia 31).
public class CalendarQuery : IValidatableObject
{
    public const int MaxDays = 100;

    // Nullable + [Required]: DateOnly "puro" nunca é null, então sem o "?" a query sem from viraria 0001-01-01 calada.
    [Required] public DateOnly? From { get; set; }
    [Required] public DateOnly? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is not { } from || To is not { } to)
            yield break; // o [Required] já reclamou

        if (to < from)
            yield return new ValidationResult("A data final não pode ser antes da inicial.", [nameof(To)]);
        // +1 porque os dois extremos contam: de 01/10 a 31/10 são 31 dias, não 30.
        else if (to.DayNumber - from.DayNumber + 1 > MaxDays)
            yield return new ValidationResult($"O intervalo pode ter no máximo {MaxDays} dias.", [nameof(To)]);
    }
}
public record CalendarDto(
    IReadOnlyList<CalendarReservationDto> Reservations,
    IReadOnlyList<CalendarBlockDto> Blocks,
    IReadOnlyList<CalendarPackageDto> Packages,
    IReadOnlyList<CalendarBufferNightDto> BufferNights);

public record CalendarReservationDto(
    Guid Id, ReservationStatus Status, DateOnly CheckIn, DateOnly CheckOut, string GuestName, decimal Total,
    string? KitName, string? PackageName, int BufferBeforeNights, int BufferAfterNights);

public record CalendarBlockDto(DateOnly Date, string? Reason);

public record CalendarPackageDto(Guid Id, string Name, DateOnly CheckIn, DateOnly CheckOut, decimal Price);

public record CalendarBufferNightDto(DateOnly Date, Guid ReservationId);