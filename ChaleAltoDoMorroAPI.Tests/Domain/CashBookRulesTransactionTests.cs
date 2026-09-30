using System.Globalization;
using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.CashScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// REGRAS_CAIXA.md, tabela "Transação" (a parte que não depende de HTTP).
public class CashBookRulesTransactionTests
{
    [Theory]
    [InlineData("2026-10-01")]
    [InlineData("2026-10-31")]
    public void Data_dentro_do_mes_do_livro_passa(string date)
    {
        var error = CashBookRules.ValidateTransaction(October(), D(date), CashTransactionType.Expense, 50m, Energia());

        Assert.Null(error);
    }

    [Theory]
    [InlineData("2026-11-01")]
    [InlineData("2026-09-30")]
    public void Data_fora_do_mes_do_livro_e_recusada(string date)
    {
        var error = CashBookRules.ValidateTransaction(October(), D(date), CashTransactionType.Expense, 50m, Energia());

        Assert.IsType<DateOutsideBook>(error);
    }

    // decimal não pode ir direto no [InlineData] (não é constante de atributo), por isso vai como texto.
    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    [InlineData("10.005")]
    [InlineData("10000000.00")]
    public void Valor_invalido_e_recusado(string amount)
    {
        var error = CashBookRules.ValidateTransaction(October(), D("2026-10-05"), CashTransactionType.Expense, Money(amount), Energia());

        Assert.IsType<InvalidAmount>(error);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("262.40")]
    [InlineData("9999999.99")]
    public void Valor_valido_passa(string amount)
    {
        var error = CashBookRules.ValidateTransaction(October(), D("2026-10-05"), CashTransactionType.Expense, Money(amount), Energia());

        Assert.Null(error);
    }

    [Fact]
    public void Saida_com_categoria_de_entrada_e_recusada()
    {
        var error = CashBookRules.ValidateTransaction(October(), D("2026-10-05"), CashTransactionType.Expense, 50m, Hospedagem());

        Assert.IsType<CategoryTypeMismatch>(error);
    }

    [Fact]
    public void Categoria_desativada_em_lancamento_novo_e_recusada()
    {
        var energia = Energia();
        energia.IsActive = false;

        var error = CashBookRules.ValidateTransaction(October(), D("2026-10-05"), CashTransactionType.Expense, 50m, energia);

        Assert.IsType<CategoryInactive>(error);
    }

    [Fact]
    public void Editar_transacao_antiga_mantendo_a_categoria_desativada_passa()
    {
        var energia = Energia();
        energia.IsActive = false;

        var error = CashBookRules.ValidateTransaction(October(), D("2026-10-05"), CashTransactionType.Expense, 262.40m, energia,
            currentCategoryId: energia.Id);

        Assert.Null(error);
    }

    [Fact]
    public void Editar_trocando_para_uma_categoria_desativada_e_recusado()
    {
        var internet = Internet();
        internet.IsActive = false;

        var error = CashBookRules.ValidateTransaction(October(), D("2026-10-05"), CashTransactionType.Expense, 50m, internet,
            currentCategoryId: Energia().Id);

        Assert.IsType<CategoryInactive>(error);
    }

    [Fact]
    public void Lancar_ou_editar_em_livro_fechado_e_recusado()
    {
        var error = CashBookRules.ValidateTransaction(October(CashBookStatus.Closed), D("2026-10-05"), CashTransactionType.Expense, 50m, Energia());

        Assert.IsType<BookClosed>(error);
    }

    [Fact]
    public void Reabrir_e_editar_passa()
    {
        var book = October(CashBookStatus.Closed);

        Assert.True(book.TryReopen(Now));
        Assert.Null(CashBookRules.ValidateTransaction(book, D("2026-10-05"), CashTransactionType.Expense, 50m, Energia()));
    }

    private static decimal Money(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
