using IPNetwork = System.Net.IPNetwork;
using Microsoft.AspNetCore.HttpOverrides;

namespace ChaleAltoDoMorroAPI.Global;

// IP real do visitante atrás do Caddy (Task 12). Sem isso, toda requisição chega com o IP do Caddy e o rate limiting
// (RateLimitPolicies.ClientKey) coloca todo mundo na mesma cota: 5 senhas erradas de alguém bloqueiam o login de todos.
public static class ForwardedHeadersSetup
{
    public static void Configure(ForwardedHeadersOptions options)
    {
        // X-Forwarded-For = IP do visitante. X-Forwarded-Proto = "https" (o Caddy recebe em HTTPS e repassa em HTTP).
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Só acredita nesses headers quando a conexão vem das redes internas do Docker, onde mora o Caddy.
        // Quem chamar a API direto (de fora) com um X-Forwarded-For inventado é ignorado.
        options.KnownIPNetworks.Add(IPNetwork.Parse("172.16.0.0/12"));
        options.KnownIPNetworks.Add(IPNetwork.Parse("192.168.0.0/16"));

        // Um proxy só (o Caddy): vale o último IP da lista, que foi o Caddy quem escreveu.
        options.ForwardLimit = 1;
    }
}
