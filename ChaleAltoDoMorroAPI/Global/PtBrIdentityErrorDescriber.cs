using Microsoft.AspNetCore.Identity;

namespace ChaleAltoDoMorroAPI.Global;

// O Identity monta as mensagens de erro por esta classe (em inglês por padrão).
// Só sobrescrevemos as que o portal pode mostrar: senha e e-mail.
public class PtBrIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordMismatch() => new()
    {
        Code = nameof(PasswordMismatch),
        Description = "Senha atual incorreta."
    };

    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = $"A senha precisa ter pelo menos {length} caracteres."
    };

    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit),
        Description = "A senha precisa ter pelo menos um número (0-9)."
    };

    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower),
        Description = "A senha precisa ter pelo menos uma letra minúscula (a-z)."
    };

    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper),
        Description = "A senha precisa ter pelo menos uma letra maiúscula (A-Z)."
    };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric),
        Description = "A senha precisa ter pelo menos um símbolo (ex.: ! @ # $)."
    };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new()
    {
        Code = nameof(PasswordRequiresUniqueChars),
        Description = $"A senha precisa ter pelo menos {uniqueChars} caracteres diferentes."
    };

    public override IdentityError DuplicateEmail(string email) => new()
    {
        Code = nameof(DuplicateEmail),
        Description = "Este e-mail já está em uso."
    };
}
