using System.ComponentModel.DataAnnotations;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Validation;

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

// Hóspede no formulário do portal. Mesmas validações de formato do site; o CPF obrigatório do hóspede 1
// é conferido no SaveReservationRequest (aqui a classe serve para os dois hóspedes).
public sealed class AdminGuestRequest : IValidatableObject
{
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150, ErrorMessage = "Nome muito longo.")]
    public string Name { get; init; } = string.Empty;

    [Cpf]
    public string? Cpf { get; init; }

    [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Telefone deve ter 10 ou 11 dígitos (DDD + número).")]
    public string? Phone { get; init; }

    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(254)]
    public string? Email { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name.Trim().Length < 2)
            yield return new ValidationResult("Nome precisa ter pelo menos 2 caracteres.", [nameof(Name)]);
    }
}

// PUT /api/admin/reservations/{id}: o formulário inteiro (substitui tudo).
public class SaveReservationRequest : IValidatableObject
{
    public DateOnly CheckIn { get; init; }
    public DateOnly CheckOut { get; init; }

    [Required(ErrorMessage = "Informe o hóspede 1.")]
    public AdminGuestRequest Guest1 { get; init; } = null!;

    public AdminGuestRequest? Guest2 { get; init; }

    public Guid? KitId { get; init; }

    /// <summary>Valor combinado. null = usar o calculado pelo BookingRules.</summary>
    [Range(typeof(decimal), "0", "999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "O total precisa ficar entre 0 e 999.999.")]
    public decimal? Total { get; init; }

    [StringLength(2000)]
    public string? AdminNotes { get; init; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Mesma regra do site: o hóspede 1 precisa de CPF e telefone (identifica a pessoa e é o contato no WhatsApp).
        if (Guest1 is null)
            yield break;
        if (string.IsNullOrWhiteSpace(Guest1.Cpf))
            yield return new ValidationResult("Informe o CPF do hóspede 1.", [$"{nameof(Guest1)}.{nameof(AdminGuestRequest.Cpf)}"]);
        if (string.IsNullOrWhiteSpace(Guest1.Phone))
            yield return new ValidationResult("Informe o telefone do hóspede 1.", [$"{nameof(Guest1)}.{nameof(AdminGuestRequest.Phone)}"]);
    }
}

// POST /api/admin/reservations: o mesmo formulário + o status inicial.
public sealed class CreateReservationRequest : SaveReservationRequest
{
    /// <summary>pending ou confirmed. Sem valor = pending.</summary>
    public ReservationStatus Status { get; init; } = ReservationStatus.Pending;

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var error in base.Validate(validationContext))
            yield return error;

        if (Status is not (ReservationStatus.Pending or ReservationStatus.Confirmed))
            yield return new ValidationResult("O status inicial precisa ser pending ou confirmed.", [nameof(Status)]);
    }
}

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
    DateTimeOffset? CancelledAt,
    int? BufferBeforeNights,        // folga gravada na confirmação (task 16); null enquanto não confirmada
    int? BufferAfterNights,
    ReservationSource Source);

public record GuestReservationDetailDto(Guid Id, string Name, string? Cpf, string? Phone, string? Email);

public record NightBreakdownDto(DateOnly Date, NightKind Kind, decimal Price);

// Detalhe = tudo da lista + o resto. Herda do item da lista pelo construtor de cópia que todo record ganha,
// então os campos não se repetem aqui e o JSON sai plano (sem um sub-objeto "item").
public record ReservationDetailDto : ReservationListItemDto
{
    public ReservationDetailDto(ReservationListItemDto item) : base(item) { }

    public required GuestReservationDetailDto Guest1 { get; init; }
    public GuestReservationDetailDto? Guest2 { get; init; }
    public string? ContactEmail { get; init; }
    public string? Observations { get; init; }
    public string? AdminNotes { get; init; }
    public Guid? PackageId { get; init; }
    public Guid? KitId { get; init; }
    public decimal? KitPrice { get; init; }
    public decimal NightsTotal { get; init; }
    public decimal CalculatedTotal { get; init; }
    public bool PriceAdjusted { get; init; }
    public decimal? EstimatedTotalFromSite { get; init; }
    public bool SiteTotalDiverged { get; init; }
    public ReservationAttributionDto? Attribution { get; init; }
    public required IReadOnlyList<NightBreakdownDto> NightsBreakdown { get; init; }
}

// Origem da pré-reserva (UTM). null = origem desconhecida (reserva do portal ou site sem essa informação).
public record ReservationAttributionDto(
    string Source,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmContent,
    string? UtmTerm,
    string? Fbclid,
    DateTimeOffset? FirstVisitAt);

// Corpo opcional do POST /confirm (task 16). null (ou corpo vazio) = usar o global de settings/buffer.
public record ConfirmReservationRequest(
    [Range(0, 3, ErrorMessage = "A folga vai de 0 a 3 noites.")] int? BufferBeforeNights,
    [Range(0, 3, ErrorMessage = "A folga vai de 0 a 3 noites.")] int? BufferAfterNights);
