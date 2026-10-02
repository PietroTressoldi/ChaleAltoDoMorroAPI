using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Meses do período no bloco "cash" do dashboard (task 19): lista de meses, meses sem livro e o formato "AAAA-MM".
public class DashboardRulesMonthsTests
{
    private static string[] Keys(IEnumerable<DateOnly> months) => months.Select(DashboardRules.MonthKey).ToArray();

    [Theory]
    [InlineData("2026-10-01", "2026-10-31", new[] { "2026-10" })]
    [InlineData("2026-10-01", "2026-12-31", new[] { "2026-10", "2026-11", "2026-12" })]
    [InlineData("2025-12-01", "2026-01-31", new[] { "2025-12", "2026-01" })] // virada de ano
    public void MonthsOf_lista_o_dia_1_de_cada_mes_do_periodo(string from, string to, string[] expected)
    {
        var months = DashboardRules.MonthsOf(D(from), D(to));

        Assert.Equal(expected, Keys(months));
        Assert.All(months, m => Assert.Equal(1, m.Day));
    }

    [Fact]
    public void MonthsOf_do_ano_tem_12_meses()
    {
        var months = DashboardRules.MonthsOf(D("2026-01-01"), D("2026-12-31"));

        Assert.Equal(12, months.Count);
        Assert.Equal(D("2026-01-01"), months[0]);
        Assert.Equal(D("2026-12-01"), months[^1]);
    }

    [Theory]
    [InlineData("2026-10-01", new string[0], new[] { "2026-10" })]                     // nov e dez ainda não chegaram
    [InlineData("2026-11-15", new[] { "2026-10" }, new[] { "2026-11" })]               // outubro já tem livro
    [InlineData("2027-01-05", new[] { "2026-11" }, new[] { "2026-10", "2026-12" })]    // período inteiro no passado
    [InlineData("2026-09-30", new string[0], new string[0])]                            // período inteiro no futuro
    [InlineData("2026-12-31", new[] { "2026-10", "2026-11", "2026-12" }, new string[0])] // todos com livro
    public void MonthsWithoutBook_lista_meses_ate_o_atual_sem_livro(string today, string[] withBook, string[] expected)
    {
        var books = withBook.Select(m => D($"{m}-01")).ToHashSet();

        var missing = DashboardRules.MonthsWithoutBook(D("2026-10-01"), D("2026-12-31"), D(today), books);

        Assert.Equal(expected, Keys(missing));
    }

    [Theory]
    [InlineData("2026-11-01", "2026-11")]
    [InlineData("2027-01-15", "2027-01")] // o dia é ignorado
    public void MonthKey_formata_AAAA_MM(string date, string expected)
    {
        Assert.Equal(expected, DashboardRules.MonthKey(D(date)));
    }
}
