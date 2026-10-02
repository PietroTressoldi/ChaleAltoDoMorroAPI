using System.ComponentModel.DataAnnotations;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Dtos;

// Query do dashboard (task 19): o que o [ApiController] transforma em 400 antes de chegar no controller.
public class DashboardQueryValidationTests
{
    // Devolve os nomes dos campos com erro (vazio = válido). Mesma ordem do ASP.NET: atributos, depois IValidatableObject.
    private static string[] InvalidFields(DashboardQuery query)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(query, new ValidationContext(query), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames).Distinct().Order().ToArray();
    }

    private static DashboardQuery Query(PeriodKind? period, int? year, int? index) =>
        new() { Period = period, Year = year, Index = index };

    [Theory]
    [InlineData(PeriodKind.Month, 2026, 1)]
    [InlineData(PeriodKind.Quarter, 2026, 4)]
    [InlineData(PeriodKind.Semester, 2026, 2)]
    [InlineData(PeriodKind.Year, 2026, null)] // index é ignorado no "year"
    public void Query_valida(PeriodKind period, int year, int? index)
    {
        Assert.Empty(InvalidFields(Query(period, year, index)));
    }

    [Fact]
    public void Sem_period_e_sem_year()
    {
        Assert.Equal(["Period", "Year"], InvalidFields(Query(null, null, 1)));
    }

    [Theory]
    [InlineData(PeriodKind.Month, 0)]
    [InlineData(PeriodKind.Month, 13)]
    [InlineData(PeriodKind.Quarter, 5)]
    [InlineData(PeriodKind.Quarter, null)] // trimestre sem index
    [InlineData(PeriodKind.Semester, 3)]
    public void Index_fora_da_faixa_do_tipo(PeriodKind period, int? index)
    {
        Assert.Equal(["Index"], InvalidFields(Query(period, 2026, index)));
    }

    [Fact]
    public void Ano_fora_da_faixa()
    {
        Assert.Equal(["Year"], InvalidFields(Query(PeriodKind.Month, 1999, 1)));
    }

    [Fact]
    public void Period_com_numero_que_nao_existe_no_enum()
    {
        // ?period=7 passa pelo model binding (enum aceita número), então a validação precisa barrar.
        Assert.Equal(["Period"], InvalidFields(Query((PeriodKind)7, 2026, 1)));
    }

    [Fact]
    public void ToPeriod_monta_o_periodo_da_query()
    {
        var period = Query(PeriodKind.Quarter, 2026, 4).ToPeriod();

        Assert.Equal(D("2026-10-01"), period.From);
        Assert.Equal(D("2026-12-31"), period.To);
        Assert.Equal("4º trimestre de 2026", period.Label);
    }
}
