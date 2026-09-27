using System.ComponentModel.DataAnnotations;
using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Dtos.Validation;

// [Cpf]: CPF com 11 dígitos, SÓ dígitos (é o que o contrato diz que o site manda) e dígitos verificadores válidos.
// Nulo passa: quem exige presença é o [Required] (o CPF do hóspede 2 é opcional).
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class CpfAttribute() : ValidationAttribute("CPF inválido.")
{
    public override bool IsValid(object? value) =>
        value is null || value is string cpf && cpf.Length == 11 && cpf.All(char.IsAsciiDigit) && CpfValidator.IsValid(cpf);
}
