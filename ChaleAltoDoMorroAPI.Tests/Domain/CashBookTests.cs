using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.CashScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// REGRAS_CAIXA.md, tabela "Livro". "Criar 2026-10 de novo" e "apagar livro com transação" dependem do banco: task 15.
public class CashBookTests
{
    [Theory]
    [InlineData(2026, 1, true)]
    [InlineData(2026, 12, true)]
    [InlineData(2020, 1, true)]
    [InlineData(2100, 12, true)]
    [InlineData(2026, 0, false)]
    [InlineData(2026, 13, false)]
    [InlineData(2019, 12, false)]
    [InlineData(2101, 1, false)]
    public void Periodo_do_livro(int year, int month, bool expected)
    {
        Assert.Equal(expected, CashBookRules.IsValidPeriod(year, month));
    }

    [Fact]
    public void Fechar_livro_aberto_grava_a_data()
    {
        var book = October();

        Assert.True(book.TryClose(Now));
        Assert.Equal(CashBookStatus.Closed, book.Status);
        Assert.Equal(Now, book.ClosedAt);
    }

    [Fact]
    public void Fechar_livro_fechado_nao_muda_nada()
    {
        var firstClose = Now.AddDays(-3);
        var book = October(CashBookStatus.Closed);
        book.ClosedAt = firstClose;

        Assert.False(book.TryClose(Now));
        Assert.Equal(firstClose, book.ClosedAt);
    }

    [Fact]
    public void Reabrir_livro_fechado_limpa_a_data_de_fechamento()
    {
        var book = October(CashBookStatus.Closed);
        book.ClosedAt = Now.AddDays(-3);

        Assert.True(book.TryReopen(Now));
        Assert.Equal(CashBookStatus.Open, book.Status);
        Assert.Null(book.ClosedAt);
    }

    [Fact]
    public void Reabrir_livro_aberto_e_recusado()
    {
        Assert.False(October().TryReopen(Now));
    }
}
