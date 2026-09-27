using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Sobreposição de pacotes (task 8). Pacote existente: Finados, sex 30/10 → seg 02/11 (noites 30, 31 e 01).
public class PackageRulesOverlapTests
{
    private static bool OverlapsFinados(string checkIn, string checkOut) =>
        PackageRules.Overlaps(D(checkIn), D(checkOut), Finados.CheckIn, Finados.CheckOut);

    [Theory]
    [InlineData("2026-10-30", "2026-11-02")] // exatamente as mesmas datas
    [InlineData("2026-10-28", "2026-10-31")] // começa antes e termina no meio
    [InlineData("2026-11-01", "2026-11-04")] // começa na última noite
    [InlineData("2026-10-31", "2026-11-01")] // uma noite dentro
    [InlineData("2026-10-25", "2026-11-10")] // engloba o pacote inteiro
    public void Intervalos_que_dividem_alguma_noite_se_sobrepoem(string checkIn, string checkOut)
    {
        Assert.True(OverlapsFinados(checkIn, checkOut));
    }

    [Theory]
    [InlineData("2026-11-02", "2026-11-04")] // começa no dia do check-out do Finados
    [InlineData("2026-10-27", "2026-10-30")] // termina no dia do check-in do Finados
    [InlineData("2026-11-10", "2026-11-12")] // bem depois
    [InlineData("2026-10-01", "2026-10-03")] // bem antes
    public void Intervalos_encostados_ou_separados_nao_se_sobrepoem(string checkIn, string checkOut)
    {
        Assert.False(OverlapsFinados(checkIn, checkOut));
    }

    [Fact]
    public void A_ordem_dos_intervalos_nao_importa()
    {
        var (a1, b1, a2, b2) = (D("2026-10-28"), D("2026-10-31"), Finados.CheckIn, Finados.CheckOut);

        Assert.Equal(PackageRules.Overlaps(a1, b1, a2, b2), PackageRules.Overlaps(a2, b2, a1, b1));
    }
}
