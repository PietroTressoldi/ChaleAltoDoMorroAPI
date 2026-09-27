using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Casos da tabela "CPF" de docs/REGRAS_RESERVA.md.
public class CpfValidatorTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    public void Cpf_valido_com_ou_sem_mascara(string cpf)
    {
        Assert.True(CpfValidator.IsValid(cpf));
    }

    [Theory]
    [InlineData("529.982.247-24")] // último dígito verificador errado
    [InlineData("529.982.247-15")] // primeiro dígito verificador errado
    [InlineData("111.111.111-11")] // todos iguais: passa na conta, mas é inválido
    [InlineData("000.000.000-00")]
    [InlineData("529.982.247")]    // incompleto
    [InlineData("529982247250")]   // dígitos a mais
    [InlineData("abc.def.ghi-jk")]
    public void Cpf_invalido(string cpf)
    {
        Assert.False(CpfValidator.IsValid(cpf));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cpf_vazio_eh_invalido(string? cpf)
    {
        Assert.False(CpfValidator.IsValid(cpf));
    }
}
