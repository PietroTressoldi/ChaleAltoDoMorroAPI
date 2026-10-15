using System.Buffers.Text;
using System.Security.Cryptography;
using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// O que o controller precisa para responder: o corpo (access token) e o que vai no cookie.
public record RefreshResult(LoginResponse Login, string Token, DateTimeOffset ExpiresAt, bool Persistent);

// Sessões do portal (task 29): emite, rotaciona e revoga refresh tokens. Nenhum token vai para log.
public class SrvRefreshToken(AppDbContext db, UserManager<IdentityUser> userManager, SrvToken srvToken, TimeProvider timeProvider, ILogger<SrvRefreshToken> log)
{
    public async Task<RefreshResult> Start(IdentityUser user, bool persistent, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        // Limpeza simples (sem job): vencidos não servem nem para detectar reuso.
        await db.RefreshTokens.Where(t => t.ExpiresAt <= now).ExecuteDeleteAsync(ct);

        var (entity, token) = NewToken(user.Id, Guid.NewGuid(), persistent, RefreshTokenRules.ExpiresAt(now, persistent), now);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);

        return new RefreshResult(srvToken.CreateToken(user), token, entity.ExpiresAt, persistent);
    }

    /// <summary>Troca o token do cookie por um novo da mesma família. null = 401.</summary>
    public async Task<RefreshResult?> Rotate(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        var now = timeProvider.GetUtcNow();
        var hash = RefreshTokenRules.Hash(token);
        var current = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        switch (RefreshTokenRules.Check(current, now))
        {
            case RefreshCheck.Valid:
                break;

            case RefreshCheck.Reused:
                // Dois navegadores com o MESMO token = um deles é cópia. Derruba os dois.
                log.LogWarning("Refresh token reusado: sessão {FamilyId} de {UserId} encerrada.", current!.FamilyId, current.UserId);
                await RevokeFamily(current.FamilyId, ct);
                return null;

            default:
                return null;
        }

        var user = await userManager.FindByIdAsync(current!.UserId);
        if (user is null || await userManager.IsLockedOutAsync(user))
            return null;

        // Mesmo ExpiresAt: a rotação não estende a sessão.
        var (next, nextToken) = NewToken(user.Id, current.FamilyId, current.Persistent, current.ExpiresAt, now);
        current.RevokedAt = now;
        current.ReplacedById = next.Id;
        db.RefreshTokens.Add(next);
        await db.SaveChangesAsync(ct);

        return new RefreshResult(srvToken.CreateToken(user), nextToken, next.ExpiresAt, next.Persistent);
    }

    /// <summary>Logout: encerra a sessão (família) do cookie.</summary>
    public async Task Revoke(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token))
            return;

        var hash = RefreshTokenRules.Hash(token);
        var familyId = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => (Guid?)t.FamilyId).FirstOrDefaultAsync(ct);

        if (familyId is not null)
            await RevokeFamily(familyId.Value, ct);
    }

    /// <summary>Troca de senha: encerra todas as sessões do usuário.</summary>
    public async Task RevokeAll(string userId, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private async Task RevokeFamily(Guid familyId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private static (RefreshToken Entity, string Token) NewToken(string userId, Guid familyId, bool persistent,
        DateTimeOffset expiresAt, DateTimeOffset now)
    {
        // 32 bytes do gerador criptográfico (Random comum é previsível). Base64Url: cabe no cookie sem escapar nada.
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = RefreshTokenRules.Hash(token),
            FamilyId = familyId,
            Persistent = persistent,
            CreatedAt = now,
            ExpiresAt = expiresAt,
        };

        return (entity, token);
    }
}
