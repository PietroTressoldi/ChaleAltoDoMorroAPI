using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Task 29: prazo do refresh token, detecção de reuso e hash.
public class RefreshTokenRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    private static RefreshToken Token(DateTimeOffset expiresAt, DateTimeOffset? revokedAt = null) =>
        new() { ExpiresAt = expiresAt, RevokedAt = revokedAt };

    [Fact]
    public void Sem_token_no_banco_eh_NotFound() =>
        Assert.Equal(RefreshCheck.NotFound, RefreshTokenRules.Check(null, Now));

    [Fact]
    public void Token_no_prazo_eh_valido() =>
        Assert.Equal(RefreshCheck.Valid, RefreshTokenRules.Check(Token(Now.AddMinutes(1)), Now));

    [Fact]
    public void Token_vence_no_instante_exato() =>
        Assert.Equal(RefreshCheck.Expired, RefreshTokenRules.Check(Token(Now), Now));

    [Fact]
    public void Token_revogado_eh_reuso_mesmo_dentro_do_prazo() =>
        Assert.Equal(RefreshCheck.Reused, RefreshTokenRules.Check(Token(Now.AddDays(1), revokedAt: Now.AddMinutes(-1)), Now));

    [Theory]
    [InlineData(true, 30 * 24)] // lembrar de mim: 30 dias
    [InlineData(false, 12)]     // sessão: 12 horas
    public void Prazo_depende_do_lembrar_de_mim(bool persistent, int hours) =>
        Assert.Equal(Now.AddHours(hours), RefreshTokenRules.ExpiresAt(Now, persistent));

    [Fact]
    public void Hash_eh_fixo_e_nao_eh_o_token()
    {
        var hash = RefreshTokenRules.Hash("abc");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, RefreshTokenRules.Hash("abc"));
        Assert.NotEqual(hash, RefreshTokenRules.Hash("abd"));
    }
}
