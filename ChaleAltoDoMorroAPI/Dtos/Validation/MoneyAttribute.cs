using System.ComponentModel.DataAnnotations;
using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Dtos.Validation;

// Dinheiro do caixa (regra 8): > 0, no máximo 2 casas e até 9.999.999,99.
// A regra mora no CashBookRules (testada); o atributo só a aplica no DTO para responder 400.
public class MoneyAttribute() : ValidationAttribute("O valor precisa ser maior que zero, com no máximo 2 casas decimais e até 9.999.999,99.")
{
    public override bool IsValid(object? value) => value is decimal amount && CashBookRules.IsValidAmount(amount);
}