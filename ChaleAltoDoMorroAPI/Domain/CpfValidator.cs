namespace ChaleAltoDoMorroAPI.Domain;

// Regra pura (sem EF, sem HTTP): valida os dígitos verificadores do CPF.
// Aceita com ou sem máscara ("529.982.247-25" ou "52998224725"). Algoritmo em docs/REGRAS_RESERVA.md.
public static class CpfValidator
{
    public static bool IsValid(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        // Só dígitos e os caracteres da máscara; qualquer outra coisa invalida.
        if (cpf.Any(c => !char.IsAsciiDigit(c) && c != '.' && c != '-'))
            return false;

        var digits = cpf.Where(char.IsAsciiDigit).Select(c => c - '0').ToArray();

        if (digits.Length != 11)
            return false;

        // 111.111.111-11 e parecidos passam na conta, mas não são CPFs válidos.
        if (digits.All(d => d == digits[0]))
            return false;

        return digits[9] == CheckDigit(digits, length: 9)
            && digits[10] == CheckDigit(digits, length: 10);
    }

    // Soma os primeiros "length" dígitos com pesos decrescentes (length + 1 ... 2).
    private static int CheckDigit(int[] digits, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += digits[i] * (length + 1 - i);

        var digit = sum * 10 % 11;
        return digit == 10 ? 0 : digit;
    }
}
