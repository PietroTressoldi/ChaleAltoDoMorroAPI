using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.CashScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// REGRAS_CAIXA.md, tabela "Resumo".
public class CashBookRulesSummaryTests
{
    [Fact]
    public void Cenario_de_referencia_soma_entradas_saidas_e_liquido()
    {
        var summary = CashBookRules.Summarize(ReferenceTransactions());

        Assert.Equal(1968m, summary.Income);
        Assert.Equal(670m, summary.Expense);
        Assert.Equal(1298m, summary.Net);
    }

    [Fact]
    public void ByCategory_traz_o_total_de_cada_categoria_usada()
    {
        var summary = CashBookRules.Summarize(ReferenceTransactions());

        var totals = summary.ByCategory.ToDictionary(c => c.CategoryId, c => c.Total);
        Assert.Equal(5, totals.Count);
        Assert.Equal(1789m, totals[Hospedagem().Id]);
        Assert.Equal(179m, totals[Kits().Id]);
        Assert.Equal(250m, totals[Energia().Id]);
        Assert.Equal(120m, totals[Internet().Id]);
        Assert.Equal(300m, totals[Limpeza().Id]);
    }

    [Fact]
    public void Daily_tem_so_os_dias_com_movimento_em_ordem()
    {
        // Entrada embaralhada de propósito: a ordem tem que vir da regra, não da lista.
        var shuffled = Enumerable.Reverse(ReferenceTransactions());

        var summary = CashBookRules.Summarize(shuffled);

        DailyTotal[] expected =
        [
            new(D("2026-10-05"), 0m, 250m),
            new(D("2026-10-09"), 1968m, 0m),
            new(D("2026-10-10"), 0m, 120m),
            new(D("2026-10-12"), 0m, 300m),
        ];
        Assert.Equal(expected, summary.Daily);
    }

    [Fact]
    public void So_saidas_deixa_o_liquido_negativo()
    {
        var summary = CashBookRules.Summarize(
        [
            Transaction("2026-10-05", Energia(), 250m),
            Transaction("2026-10-10", Internet(), 120m),
        ]);

        Assert.Equal(0m, summary.Income);
        Assert.Equal(370m, summary.Expense);
        Assert.Equal(-370m, summary.Net);
    }

    [Fact]
    public void Livro_vazio_zera_tudo()
    {
        var summary = CashBookRules.Summarize([]);

        Assert.Equal(0m, summary.Income);
        Assert.Equal(0m, summary.Expense);
        Assert.Equal(0m, summary.Net);
        Assert.Empty(summary.ByCategory);
        Assert.Empty(summary.Daily);
    }

    [Fact]
    public void Centavos_somam_exato()
    {
        var summary = CashBookRules.Summarize(
        [
            Transaction("2026-10-05", Hospedagem(), 0.10m),
            Transaction("2026-10-06", Hospedagem(), 0.20m),
        ]);

        Assert.Equal(0.30m, summary.Income);
    }
}
