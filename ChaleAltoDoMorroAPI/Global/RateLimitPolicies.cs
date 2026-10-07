using System.Threading.RateLimiting;

namespace ChaleAltoDoMorroAPI.Global;

// Nomes das políticas (usados no [EnableRateLimiting]) e a chave de partição num lugar só.
public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Password = "password";
    public const string PreReservation = "pre-reservation";

    // Quem é "o cliente": o IP. Atrás do Caddy, o RemoteIpAddress só é o do visitante porque o UseForwardedHeaders
    // (ForwardedHeadersSetup) roda ANTES do UseRateLimiter. Sem isso, todo mundo dividiria a mesma cota.
    public static string ClientKey(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

    public static RateLimitPartition<string> FixedWindowPerClient(HttpContext http, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(http), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0   // estourou → 429 na hora, sem fila
        });
}
