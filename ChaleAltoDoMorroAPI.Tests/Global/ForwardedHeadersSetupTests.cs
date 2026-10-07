using System.Net;
using ChaleAltoDoMorroAPI.Global;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChaleAltoDoMorroAPI.Tests.Global;

// IP real atrás do Caddy (task 12): roda o middleware de verdade com a configuração da API.
public class ForwardedHeadersSetupTests
{
    private static async Task<HttpContext> RunAsync(string remoteIp, string forwardedFor)
    {
        var options = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(options);

        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));

        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await middleware.Invoke(context);
        return context;
    }

    [Theory]
    [InlineData("172.18.0.3")]          // Caddy numa rede do compose
    [InlineData("::ffff:172.18.0.3")]   // o mesmo IP como o Kestrel vê no container (IPv4 dentro de IPv6)
    [InlineData("192.168.16.2")]        // faixa que o Docker usa quando as 172.x acabam
    public async Task Vindo_do_Caddy_usa_o_IP_do_visitante_e_https(string caddyIp)
    {
        var context = await RunAsync(caddyIp, "203.0.113.5");

        Assert.Equal(IPAddress.Parse("203.0.113.5"), context.Connection.RemoteIpAddress);
        Assert.Equal("https", context.Request.Scheme);
    }

    [Fact]
    public async Task Vindo_de_fora_ignora_o_header_inventado()
    {
        var context = await RunAsync("198.51.100.7", "203.0.113.5");

        Assert.Equal(IPAddress.Parse("198.51.100.7"), context.Connection.RemoteIpAddress);
        Assert.Equal("http", context.Request.Scheme);
    }

    [Fact]
    public async Task Com_lista_de_IPs_vale_o_ultimo_que_o_Caddy_escreveu()
    {
        var context = await RunAsync("172.18.0.3", "1.2.3.4, 203.0.113.5");

        Assert.Equal(IPAddress.Parse("203.0.113.5"), context.Connection.RemoteIpAddress);
    }
}
