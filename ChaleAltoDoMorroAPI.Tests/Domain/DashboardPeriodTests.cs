using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Períodos do dashboard (task 19): limites, nome, período anterior e índices inválidos.
public class DashboardPeriodTests
{
    [Theory]
    [InlineData(PeriodKind.Month, 2026, 10, "2026-10-01", "2026-10-31", "outubro de 2026")]
    [InlineData(PeriodKind.Month, 2026, 2, "2026-02-01", "2026-02-28", "fevereiro de 2026")]
    [InlineData(PeriodKind.Month, 2028, 2, "2028-02-01", "2028-02-29", "fevereiro de 2028")] // bissexto
    [InlineData(PeriodKind.Quarter, 2026, 4, "2026-10-01", "2026-12-31", "4º trimestre de 2026")]
    [InlineData(PeriodKind.Quarter, 2026, 1, "2026-01-01", "2026-03-31", "1º trimestre de 2026")]
    [InlineData(PeriodKind.Semester, 2026, 1, "2026-01-01", "2026-06-30", "1º semestre de 2026")]
    [InlineData(PeriodKind.Semester, 2026, 2, "2026-07-01", "2026-12-31", "2º semestre de 2026")]
    [InlineData(PeriodKind.Year, 2026, 1, "2026-01-01", "2026-12-31", "2026")]
    public void Of_calcula_limites_e_nome(PeriodKind kind, int year, int index, string from, string to, string label)
    {
        var period = DashboardPeriod.Of(kind, year, index)!;

        Assert.Equal(D(from), period.From);
        Assert.Equal(D(to), period.To);
        Assert.Equal(label, period.Label);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Year_ignora_o_index(int index)
    {
        var period = DashboardPeriod.Of(PeriodKind.Year, 2026, index);

        Assert.NotNull(period);
        Assert.Equal(1, period.Index);
    }

    [Theory]
    [InlineData(PeriodKind.Month, 2026, 10, 2026, 9)]
    [InlineData(PeriodKind.Month, 2026, 1, 2025, 12)]   // virada de ano
    [InlineData(PeriodKind.Quarter, 2026, 4, 2026, 3)]
    [InlineData(PeriodKind.Quarter, 2026, 1, 2025, 4)]  // virada de ano
    [InlineData(PeriodKind.Semester, 2026, 2, 2026, 1)]
    [InlineData(PeriodKind.Semester, 2026, 1, 2025, 2)] // virada de ano
    [InlineData(PeriodKind.Year, 2026, 1, 2025, 1)]
    public void Previous_e_o_periodo_imediatamente_anterior(PeriodKind kind, int year, int index, int prevYear, int prevIndex)
    {
        var previous = DashboardPeriod.Of(kind, year, index)!.Previous();

        Assert.Equal(kind, previous.Kind);
        Assert.Equal(prevYear, previous.Year);
        Assert.Equal(prevIndex, previous.Index);
    }

    [Fact]
    public void Previous_de_janeiro_vai_ate_31_de_dezembro()
    {
        var previous = DashboardPeriod.Of(PeriodKind.Month, 2026, 1)!.Previous();

        Assert.Equal(D("2025-12-01"), previous.From);
        Assert.Equal(D("2025-12-31"), previous.To);
    }

    [Theory]
    [InlineData(PeriodKind.Month, 2026, 0)]
    [InlineData(PeriodKind.Month, 2026, 13)]
    [InlineData(PeriodKind.Quarter, 2026, 0)]
    [InlineData(PeriodKind.Quarter, 2026, 5)]
    [InlineData(PeriodKind.Semester, 2026, 3)]
    [InlineData(PeriodKind.Month, 1999, 1)] // ano fora da faixa
    public void Of_com_ano_ou_index_invalido_devolve_null(PeriodKind kind, int year, int index)
    {
        Assert.Null(DashboardPeriod.Of(kind, year, index));
    }
}
