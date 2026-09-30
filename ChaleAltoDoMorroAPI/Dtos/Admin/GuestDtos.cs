using System.ComponentModel.DataAnnotations;

namespace ChaleAltoDoMorroAPI.Dtos.Admin;


public enum GuestSort { Name, Stays, TotalSpent, LastStay }

public class GuestQuery : IValidatableObject
{
    [StringLength(100)] public string? Search { get; set; }
    public GuestSort? Sort { get; set; }
    public SortOrder? Order { get; set; }
    [Range(1, 100_000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Sort is { } sort && !Enum.IsDefined(sort))
            yield return new ValidationResult("Sort inválido. Use name, stays, totalSpent ou lastStay.", [nameof(Sort)]);
        if (Order is { } order && !Enum.IsDefined(order))
            yield return new ValidationResult("Order inválido. Use asc ou desc.", [nameof(Order)]);
    }
}

// Propriedades com init (e não record posicional): o EF só consegue ORDENAR por um campo calculado
// se a projeção for "new X { Prop = ... }". Com construtor, ele não sabe qual parâmetro vira qual propriedade.
public record GuestListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public int Stays { get; init; }
    public decimal TotalSpent { get; init; }
    public DateOnly? LastStay { get; init; }
    public DateOnly? NextStay { get; init; }
}


// Detalhe = item da lista + CPF, notas e histórico (mesmo truque do ReservationDetailDto).
public record GuestDetailDto : GuestListItemDto
{
    public GuestDetailDto(GuestListItemDto item) : base(item) { }

    public string? Cpf { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<ReservationListItemDto> Reservations { get; init; } = [];
}

// PATCH /api/admin/guests/{id}: o formulário manda os 4 campos; vazio apaga (e-mail, telefone, notas).
// O CPF não entra: não é editável pelo portal nesta versão.
public sealed class UpdateGuestRequest : IValidatableObject
{
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150, ErrorMessage = "Nome muito longo.")]
    public string Name { get; init; } = string.Empty;

    [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Telefone deve ter 10 ou 11 dígitos (DDD + número).")]
    public string? Phone { get; init; }

    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(254)]
    public string? Email { get; init; }

    [StringLength(1000, ErrorMessage = "Notas com no máximo 1000 caracteres.")]
    public string? Notes { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Name.Trim().Length < 2)
            yield return new ValidationResult("Nome precisa ter pelo menos 2 caracteres.", [nameof(Name)]);
    }
}
