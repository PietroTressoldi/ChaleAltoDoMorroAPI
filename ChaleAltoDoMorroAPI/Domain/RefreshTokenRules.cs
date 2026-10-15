using System.Security.Cryptography;
using System.Text;

namespace ChaleAltoDoMorroAPI.Domain;

// Regras do refresh token PURAS (task 29): sem EF, sem HTTP, "agora" chega por parâmetro.
public static class RefreshTokenRules
{
    public static readonly TimeSpan RememberLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);

    /// <summary>Prazo ABSOLUTO, contado do login. A rotação copia este valor: renovar não estende a sessão.</summary>
    public static DateTimeOffset ExpiresAt(DateTimeOffset now, bool persistent) =>
        now + (persistent ? RememberLifetime : SessionLifetime);

    /// <summary>Revogado vem antes de vencido: token revogado chegando de novo = alguém copiou o cookie.</summary>
    public static RefreshCheck Check(RefreshToken? token, DateTimeOffset now) =>
        token is null ? RefreshCheck.NotFound
        : token.RevokedAt is not null ? RefreshCheck.Reused
        : token.ExpiresAt <= now ? RefreshCheck.Expired
        : RefreshCheck.Valid;

    /// <summary>SHA-256 em hexa (64 caracteres). O banco só guarda isto, nunca o token.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public enum RefreshCheck
{
    Valid,
    NotFound,
    Expired,
    Reused
}