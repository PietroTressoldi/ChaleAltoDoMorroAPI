using System.ComponentModel.DataAnnotations;
using ChaleAltoDoMorroAPI.Dtos.Validation;

namespace ChaleAltoDoMorroAPI.Dtos;

// Corpo do POST /api/pre-reservations, idêntico ao docs/CONTRATO_SITE.md.
// Aqui só se valida FORMATO. Datas, preço, pacote e kit são regras de negócio: ficam no ReservationService.
public sealed class PostPreReservationRequest
{
    // DateOnly ausente no JSON vira default (01/01/0001): o BookingRules.ValidateStay devolve InvalidDate.
    public DateOnly CheckIn { get; init; }
    public DateOnly CheckOut { get; init; }

    /// <summary>Informativo: o servidor recalcula.</summary>
    public int? Nights { get; init; }

    public Guid? PackageId { get; init; }

    /// <summary>Kits escolhidos. Um kit por reserva (decisão do dono): [] ou [id].</summary>
    [MaxLength(1, ErrorMessage = "Escolha no máximo um kit por reserva.")]
    public List<Guid> ExtraIds { get; init; } = [];

    /// <summary>Só informativo. NUNCA usar como preço: o total gravado é o recalculado.</summary>
    public decimal? EstimatedTotal { get; init; }

    [Required(ErrorMessage = "Informe o hóspede.")]
    public GuestDto Guest { get; init; } = null!;

    public SecondGuestDto? SecondGuest { get; init; }

    /// <summary>De onde o visitante veio (UTM). Opcional: site antigo ou navegador sem armazenamento manda null.</summary>
    public AttributionDto? Attribution { get; init; }
}

// Origem da pré-reserva. SEM atributos de validação de propósito: dado de marketing nunca pode virar 400.
// Tudo é texto (até a data) e quem limpa é o AttributionRules.Normalize, no SrvReservation.
public sealed class AttributionDto
{
    /// <summary>meta_ads | instagram | google | direct | other. Outro valor é gravado como "other".</summary>
    public string? Source { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmMedium { get; init; }
    public string? UtmCampaign { get; init; }
    public string? UtmContent { get; init; }
    public string? UtmTerm { get; init; }

    /// <summary>ID do clique da Meta (parâmetro fbclid que o Facebook/Instagram coloca no link).</summary>
    public string? Fbclid { get; init; }

    /// <summary>Primeira visita (ISO 8601, UTC). Texto para que uma data inválida não derrube o binding.</summary>
    public string? FirstVisitAt { get; init; }
}

public sealed class GuestDto : IValidatableObject
{
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150, ErrorMessage = "Nome muito longo.")]
    public string Name { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o CPF.")]
    [Cpf]
    public string Cpf { get; init; } = string.Empty;

    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(254)]
    public string? Email { get; init; }

    [Required(ErrorMessage = "Informe o telefone.")]
    [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Telefone deve ter 10 ou 11 dígitos (DDD + número).")]
    public string Phone { get; init; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Observações com no máximo 1000 caracteres.")]
    public string? Notes { get; init; }

    // Regra que atributo não expressa: mínimo de 2 caracteres SEM contar espaços nas pontas.
    // Só roda depois que os atributos passaram (o Name já não é nulo aqui).
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name.Trim().Length < 2)
            yield return new ValidationResult("Nome precisa ter pelo menos 2 caracteres.", [nameof(Name)]);
    }
}

public sealed class SecondGuestDto : IValidatableObject
{
    [StringLength(150, ErrorMessage = "Nome muito longo.")]
    public string? Name { get; init; }

    [Cpf]
    public string? Cpf { get; init; }

    // Hóspede 2 é opcional, mas se tiver CPF precisa ter nome (e nome, se vier, com 2+ caracteres).
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var name = Name?.Trim() ?? string.Empty;

        if (Cpf is not null && name.Length == 0)
            yield return new ValidationResult("Informe o nome do segundo hóspede.", [nameof(Name)]);
        else if (name.Length == 1)
            yield return new ValidationResult("Nome precisa ter pelo menos 2 caracteres.", [nameof(Name)]);
    }
}
