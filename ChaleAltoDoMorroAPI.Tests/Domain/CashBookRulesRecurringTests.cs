using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.CashScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// REGRAS_CAIXA.md, tabelas "Data da despesa fixa" e "Aplicar despesas fixas".
public class CashBookRulesRecurringTests
{
    [Theory]
    [InlineData(10, 2026, 10, "2026-10-10")]
    [InlineData(31, 2026, 10, "2026-10-31")]
    [InlineData(31, 2026, 11, "2026-11-30")]
    [InlineData(31, 2027, 2, "2027-02-28")]
    [InlineData(30, 2028, 2, "2028-02-29")] // bissexto
    [InlineData(1, 2026, 12, "2026-12-01")]
    public void Dia_da_despesa_fixa_vira_o_ultimo_dia_quando_o_mes_e_curto(int day, int year, int month, string expected)
    {
        Assert.Equal(D(expected), CashBookRules.RecurringDate(day, year, month));
    }

    [Fact]
    public void Aplicar_em_outubro_cria_so_a_Luz()
    {
        var plan = CashBookRules.RecurringToApply(RecurringModels(), 2026, 10, new HashSet<int>());

        Assert.Equal(new[] { "Luz" }, plan.ToCreate.Select(m => m.Description));
        Assert.Equal(0, plan.Skipped);
    }

    [Fact]
    public void Aplicar_de_novo_nao_duplica()
    {
        var plan = CashBookRules.RecurringToApply(RecurringModels(), 2026, 10, new HashSet<int> { 1 });

        Assert.Empty(plan.ToCreate);
        Assert.Equal(1, plan.Skipped);
    }

    [Fact]
    public void Aplicar_em_novembro_cria_Luz_e_Internet()
    {
        var plan = CashBookRules.RecurringToApply(RecurringModels(), 2026, 11, new HashSet<int>());

        Assert.Equal(new[] { "Luz", "Internet" }, plan.ToCreate.Select(m => m.Description));
    }

    [Fact]
    public void Aplicar_em_setembro_cria_so_o_Seguro()
    {
        var plan = CashBookRules.RecurringToApply(RecurringModels(), 2026, 9, new HashSet<int>());

        Assert.Equal(new[] { "Seguro" }, plan.ToCreate.Select(m => m.Description));
    }

    [Fact]
    public void Apagar_a_Luz_lancada_e_aplicar_de_novo_ela_volta()
    {
        // Antes: a Luz já estava lançada. Depois de apagar, o id dela some da lista de "já lançados".
        var before = CashBookRules.RecurringToApply(RecurringModels(), 2026, 10, new HashSet<int> { 1 });
        var after = CashBookRules.RecurringToApply(RecurringModels(), 2026, 10, new HashSet<int>());

        Assert.Empty(before.ToCreate);
        Assert.Single(after.ToCreate);
    }

    [Fact]
    public void Transacao_gerada_copia_o_modelo_com_a_data_do_mes()
    {
        var luz = RecurringModels()[0];

        var transaction = CashBookRules.FromRecurring(luz, October(), Now);

        Assert.Equal(10, transaction.CashBookId);
        Assert.Equal(D("2026-10-10"), transaction.Date);
        Assert.Equal(CashTransactionType.Expense, transaction.Type);
        Assert.Equal(250m, transaction.Amount);
        Assert.Equal("Luz", transaction.Description);
        Assert.Equal(luz.CategoryId, transaction.CategoryId);
        Assert.Equal(luz.PaymentMethod, transaction.PaymentMethod);
        Assert.Equal(luz.Id, transaction.RecurringExpenseId);
    }

    [Fact]
    public void Editar_o_valor_lancado_nao_muda_o_modelo()
    {
        var luz = RecurringModels()[0];
        var transaction = CashBookRules.FromRecurring(luz, October(), Now);

        transaction.Amount = 262.40m;

        Assert.Equal(250m, luz.Amount);
    }
}
