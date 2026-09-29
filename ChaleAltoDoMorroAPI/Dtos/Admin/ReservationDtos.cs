using System.ComponentModel.DataAnnotations;
using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Dtos.Admin;

// Reservas do portal (task 9). Pré-reserva = reserva com status "pending": as duas telas usam estas mesmas rotas.

// Genérico: a lista de hóspedes (task 17) reaproveita.
public record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

// ---------- Filtros da lista ----------

public enum ReservationSort { CreatedAt, CheckIn, Total, GuestName }
public enum SortOrder { Asc, Desc }

// [FromQuery]: cada propriedade vira um parâmetro da URL (?status=pending&status=cancelled&sort=checkIn...).
// Enum na query é convertido sem diferenciar maiúsculas; texto desconhecido (sort=banana) já vira 400 no binding.
public class ReservationQuery : IValidatableObject
{
    public List<ReservationStatus>? Status { get; set; }
    public ReservationWhen? When { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [StringLength(100)] public string? Search { get; set; }
    public ReservationSort? Sort { get; set; }
    public SortOrder? Order { get; set; }
    [Range(1, 100_000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;

    // Número também passa no binding de enum (sort=7 vira (ReservationSort)7), então conferimos aqui: 400 também.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status?.Any(s => !Enum.IsDefined(s)) == true)
            yield return new ValidationResult("Status inválido. Use pending, confirmed ou cancelled.", [nameof(Status)]);
        if (When is { } when && !Enum.IsDefined(when))
            yield return new ValidationResult("When inválido. Use today, ongoing, upcoming ou past.", [nameof(When)]);
        if (Sort is { } sort && !Enum.IsDefined(sort))
            yield return new ValidationResult("Sort inválido. Use createdAt, checkIn, total ou guestName.", [nameof(Sort)]);
        if (Order is { } order && !Enum.IsDefined(order))
            yield return new ValidationResult("Order inválido. Use asc ou desc.", [nameof(Order)]);
        if (From > To)
            yield return new ValidationResult("O início do período precisa ser antes do fim.", [nameof(From), nameof(To)]);
    }
}

// ---------- Entrada ----------

// PATCH /api/admin/reservations/{id}. Texto vazio apaga a anotação.
public record UpdateReservationNotesRequest([StringLength(2000)] string? AdminNotes);

// ---------- Saída ----------

// Lista: sem CPF, sem e-mail. Só o que a tabela do portal mostra.
public record ReservationListItemDto(
    Guid Id,
    ReservationStatus Status,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    string GuestName,
    string? SecondGuestName,
    string ContactPhone,
    string? PackageName,
    string? KitName,
    decimal Total,
    bool IncludesColonialBreakfast,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CancelledAt);

public record GuestDetailDto(Guid Id, string Name, string? Cpf, string? Phone, string? Email);

public record NightBreakdownDto(DateOnly Date, NightKind Kind, decimal Price);

// Detalhe = tudo da lista + o resto. Herda do item da lista pelo construtor de cópia que todo record ganha,
// então os campos não se repetem aqui e o JSON sai plano (sem um sub-objeto "item").
public record ReservationDetailDto : ReservationListItemDto
{
    public ReservationDetailDto(ReservationListItemDto item) : base(item) { }

    public required GuestDetailDto Guest1 { get; init; }
    public GuestDetailDto? Guest2 { get; init; }
    public string? ContactEmail { get; init; }
    public string? Observations { get; init; }
    public string? AdminNotes { get; init; }
    public Guid? PackageId { get; init; }
    public Guid? KitId { get; init; }
    public decimal? KitPrice { get; init; }
    public decimal NightsTotal { get; init; }
    public decimal EstimatedTotalFromSite { get; init; }
    public bool SiteTotalDiverged { get; init; }
    public required IReadOnlyList<NightBreakdownDto> NightsBreakdown { get; init; }
}
