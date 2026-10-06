using ChaleAltoDoMorroAPI.Global;

namespace ChaleAltoDoMorroAPI.Tests.Global;

// Origens do CORS em produção (task 12): o que o navegador manda no header Origin, e nada além disso.
public class CorsPoliciesTests
{
    [Theory]
    [InlineData("https://chalealtodomorro.com.br")]
    [InlineData("https://portal.chalealtodomorro.com.br")]
    [InlineData("https://chale.pages.dev")]
    [InlineData("https://localhost:8443")]
    public void Origem_https_sem_caminho_e_aceita(string origin)
    {
        CorsPolicies.EnsureValidOrigins("Cors:AllowedOrigins", [origin]);
    }

    [Theory]
    [InlineData("https://chalealtodomorro.com.br/")]       // barra no fim
    [InlineData("https://chalealtodomorro.com.br/reserva")] // caminho
    [InlineData("http://chalealtodomorro.com.br")]          // sem HTTPS
    [InlineData("https://ChaleAltoDoMorro.com.br")]         // maiúsculas
    [InlineData("chalealtodomorro.com.br")]                 // sem esquema
    [InlineData("")]
    public void Origem_malformada_derruba_a_subida(string origin)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CorsPolicies.EnsureValidOrigins("Cors:AllowedOrigins", [origin]));

        Assert.Contains("Cors:AllowedOrigins", ex.Message);
    }

    [Fact]
    public void Lista_vazia_aponta_a_variavel_de_ambiente()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CorsPolicies.EnsureValidOrigins("Cors:PortalOrigins", []));

        Assert.Contains("Cors__PortalOrigins__0", ex.Message);
    }
}
