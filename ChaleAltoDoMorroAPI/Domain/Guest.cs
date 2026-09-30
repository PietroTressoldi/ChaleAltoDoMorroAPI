namespace ChaleAltoDoMorroAPI.Domain;

// Cadastro de hóspedes (lista no portal). O CPF identifica a pessoa entre reservas.
// O hóspede 2 pode vir sem CPF: nesse caso cada reserva gera um cadastro novo (não há como deduplicar).
public class Guest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
}
