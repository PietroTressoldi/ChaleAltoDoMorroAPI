using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Tabelas "Preço de cada noite" e "Orçamento (quote)" de docs/REGRAS_RESERVA.md.
public class BookingRulesQuoteTests
{
    // ---------- Preço de cada noite ----------

    // decimal não é constante de atributo: o preço vem como double no [InlineData] e vira decimal no teste.
    [Theory]
    [InlineData("2026-10-06", NightKind.Weekday, 790)]    // terça
    [InlineData("2026-10-09", NightKind.Weekend, 894.50)] // sexta
    [InlineData("2026-10-10", NightKind.Weekend, 894.50)] // sábado
    [InlineData("2026-10-11", NightKind.Weekend, 894.50)] // domingo
    [InlineData("2026-10-12", NightKind.Weekday, 790)]    // segunda
    public void Preco_da_noite_fora_de_pacote(string night, NightKind kind, double price)
    {
        var result = BookingRules.PriceNight(D(night), ScenarioData());

        Assert.Equal(new NightPrice(D(night), kind, (decimal)price), result);
    }

    [Fact]
    public void Noite_de_pacote_vale_o_total_dividido_pelas_diarias()
    {
        var result = BookingRules.PriceNight(D("2026-10-31"), ScenarioData());

        Assert.Equal(new NightPrice(D("2026-10-31"), NightKind.Package, 830m, Finados.Id), result);
    }

    [Fact]
    public void Sem_preco_de_fim_de_semana_a_sexta_usa_o_de_semana()
    {
        var result = BookingRules.PriceNight(D("2026-10-09"), ScenarioData(weekend: null));

        Assert.Equal(new NightPrice(D("2026-10-09"), NightKind.Weekday, 790m), result);
    }

    [Fact]
    public void Sem_nenhum_preco_nao_inventa_valor()
    {
        Assert.Null(BookingRules.PriceNight(D("2026-10-06"), ScenarioData(weekday: null, weekend: null)));
    }

    // ---------- Orçamento ----------

    [Fact]
    public void Fim_de_semana_custa_1789_e_inclui_cesta_de_cafe()
    {
        var quote = BookingRules.Quote(D("2026-10-09"), D("2026-10-11"), ScenarioData());

        Assert.NotNull(quote);
        Assert.Equal(2, quote.Nights.Count);
        Assert.Equal([new QuoteLine(NightKind.Weekend, null, 2, 894.50m, 1789m)], quote.Lines);
        Assert.Equal(1789m, quote.NightsTotal);
        Assert.Equal(0m, quote.KitTotal);
        Assert.Equal(1789m, quote.Total);
        Assert.True(quote.IncludesColonialBreakfast);
        Assert.Null(quote.Package);
    }

    [Fact]
    public void Semana_mais_fim_de_semana_mais_kit()
    {
        // qui (645, estadia de 2+) + sex e sáb (2 × 894,50) + Kit Toque de Romance (179)
        var quote = BookingRules.Quote(D("2026-10-08"), D("2026-10-11"), ScenarioData(), ToqueDeRomance);

        Assert.NotNull(quote);
        Assert.Equal(
            [
                new QuoteLine(NightKind.Weekday, null, 1, 645m, 645m),
                new QuoteLine(NightKind.Weekend, null, 2, 894.50m, 1789m),
            ],
            quote.Lines);
        Assert.Equal(2434m, quote.NightsTotal);
        Assert.Equal(ToqueDeRomance, quote.Kit);
        Assert.Equal(179m, quote.KitTotal);
        Assert.Equal(2613m, quote.Total);
    }

    [Fact]
    public void Pacote_vira_uma_linha_so_com_o_preco_fechado()
    {
        var quote = BookingRules.Quote(D("2026-10-30"), D("2026-11-02"), ScenarioData());

        Assert.NotNull(quote);
        Assert.Equal([new QuoteLine(NightKind.Package, "Finados", 3, null, 2490m)], quote.Lines);
        Assert.Equal(2490m, quote.Total);
        Assert.Equal(Finados, quote.Package);
    }

    [Fact]
    public void Uma_diaria_nao_inclui_cesta_de_cafe()
    {
        var quote = BookingRules.Quote(D("2026-10-06"), D("2026-10-07"), ScenarioData());

        Assert.NotNull(quote);
        Assert.False(quote.IncludesColonialBreakfast);
    }

    [Fact]
    public void Valores_quebrados_somam_certo()
    {
        // 3 noites de semana a 894,50 (o JavaScript precisa arredondar; decimal já é exato).
        var quote = BookingRules.Quote(D("2026-10-06"), D("2026-10-09"), ScenarioData(weekday: 894.50m, weekend: null, longStay: null));

        Assert.NotNull(quote);
        Assert.Equal(2683.50m, quote.Total);
    }

    // ---------- Patch 1 (task 23): desconto de 2+ diárias, domingo e diária estendida ----------

    [Fact]
    public void Noite_de_semana_em_estadia_longa_tem_desconto()
    {
        var result = BookingRules.PriceNight(D("2026-10-06"), ScenarioData(), longStay: true);

        Assert.Equal(new NightPrice(D("2026-10-06"), NightKind.Weekday, 645m), result);
    }

    [Fact]
    public void Desconto_nao_vale_para_noite_de_fim_de_semana()
    {
        var result = BookingRules.PriceNight(D("2026-10-09"), ScenarioData(), longStay: true);

        Assert.Equal(new NightPrice(D("2026-10-09"), NightKind.Weekend, 894.50m), result);
    }

    [Fact]
    public void Duas_diarias_na_semana_custam_1290_com_cesta()
    {
        var quote = BookingRules.Quote(D("2026-10-05"), D("2026-10-07"), ScenarioData());

        Assert.NotNull(quote);
        Assert.Equal([new QuoteLine(NightKind.Weekday, null, 2, 645m, 1290m)], quote.Lines);
        Assert.True(quote.IncludesColonialBreakfast);
    }

    [Fact]
    public void Sem_desconto_cadastrado_duas_diarias_custam_o_preco_cheio()
    {
        var quote = BookingRules.Quote(D("2026-10-05"), D("2026-10-07"), ScenarioData(longStay: null));

        Assert.Equal(1580m, quote!.Total);
    }

    [Fact]
    public void Uma_diaria_na_semana_custa_o_preco_cheio()
    {
        Assert.Equal(790m, BookingRules.Quote(D("2026-10-06"), D("2026-10-07"), ScenarioData())!.Total);
    }

    [Theory]
    [InlineData("2026-10-09", "2026-10-11", 1789)]    // sex → dom
    [InlineData("2026-10-10", "2026-10-12", 1789)]    // sáb → seg
    [InlineData("2026-10-09", "2026-10-12", 2683.50)] // sex → seg
    [InlineData("2026-10-08", "2026-10-10", 1539.50)] // qui → sáb: 645 + 894,50
    public void Domingo_conta_como_fim_de_semana(string checkIn, string checkOut, double total)
    {
        Assert.Equal((decimal)total, BookingRules.Quote(D(checkIn), D(checkOut), ScenarioData())!.Total);
    }

    [Theory]
    [InlineData("2026-10-09", "2026-10-10")] // sex → sáb
    [InlineData("2026-10-10", "2026-10-11")] // sáb → dom
    public void Diaria_estendida_tem_preco_fechado_e_nao_inclui_cesta(string checkIn, string checkOut)
    {
        var quote = BookingRules.Quote(D(checkIn), D(checkOut), ScenarioData());

        Assert.NotNull(quote);
        Assert.Equal([new QuoteLine(NightKind.Extended, null, 1, 1190m, 1190m)], quote.Lines);
        Assert.Equal(1190m, quote.Total);
        Assert.False(quote.IncludesColonialBreakfast);
    }

    [Fact]
    public void Sem_regra_de_diaria_estendida_a_noite_de_sabado_e_de_fim_de_semana()
    {
        var quote = BookingRules.Quote(D("2026-10-10"), D("2026-10-11"), ScenarioData(extended: null));

        Assert.Equal(894.50m, quote!.Total);
    }

    [Fact]
    public void Sem_precos_cadastrados_nao_ha_orcamento()
    {
        Assert.Null(BookingRules.Quote(D("2026-10-06"), D("2026-10-08"), ScenarioData(weekday: null, weekend: null)));
    }
}
