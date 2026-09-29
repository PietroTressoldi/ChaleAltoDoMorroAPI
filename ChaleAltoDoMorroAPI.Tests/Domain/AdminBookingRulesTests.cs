using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Reserva criada ou editada pelo portal (task 21). Cenário de referência: 790 / 894,50, 14/10 e 24/10 bloqueadas,
// Finados de 30/10 a 02/11 por 2.490. Tabela em docs/REGRAS_RESERVA.md.
public class AdminBookingRulesTests
{
    private static StayError? Validate(string checkIn, string checkOut, params string[] unavailable) =>
        AdminBookingRules.ValidateStay(D(checkIn), D(checkOut), unavailable.Select(D).ToHashSet());

    private static AdminPrice? PriceOf(string checkIn, string checkOut, decimal? informedTotal = null, BookingData? data = null)
    {
        var quote = BookingRules.Quote(D(checkIn), D(checkOut), data ?? ScenarioData());
        return AdminBookingRules.Price(quote, informedTotal, kitPrice: 0m);
    }

    // ---------- Validação: só a disponibilidade ----------

    [Fact]
    public void Sexta_a_sabado_com_uma_diaria_e_aceito_pelo_admin()
    {
        Assert.Null(Validate("2026-10-09", "2026-10-10"));
    }

    [Fact]
    public void Datas_no_passado_sao_aceitas_para_registrar_reserva_antiga()
    {
        Assert.Null(Validate("2026-09-20", "2026-09-22"));
    }

    [Fact]
    public void Pacote_em_parte_e_aceito_pelo_admin()
    {
        Assert.Null(Validate("2026-10-31", "2026-11-01"));
    }

    [Fact]
    public void Noite_indisponivel_e_recusada_apontando_a_noite()
    {
        var error = Validate("2026-10-13", "2026-10-15", "2026-10-14");

        Assert.Equal(new BlockedNight(D("2026-10-14")), error);
    }

    [Fact]
    public void Dia_do_check_out_indisponivel_nao_atrapalha()
    {
        // A noite do check-out não é da estadia: 12/10 ocupada não impede 10/10 → 12/10.
        Assert.Null(Validate("2026-10-10", "2026-10-12", "2026-10-12"));
    }

    [Theory]
    [InlineData("2026-10-10", "2026-10-10")]
    [InlineData("2026-10-11", "2026-10-10")]
    public void Check_out_precisa_ser_depois_do_check_in(string checkIn, string checkOut)
    {
        Assert.IsType<CheckOutNotAfterCheckIn>(Validate(checkIn, checkOut));
    }

    [Fact]
    public void Datas_ausentes_sao_invalidas()
    {
        var error = AdminBookingRules.ValidateStay(default, D("2026-10-10"), new HashSet<DateOnly>());

        Assert.IsType<InvalidDate>(error);
    }

    // ---------- Preço ----------

    [Fact]
    public void Sem_total_informado_vale_o_calculado()
    {
        var price = PriceOf("2026-10-09", "2026-10-10");

        Assert.Equal(new AdminPrice(Total: 894.50m, CalculatedTotal: 894.50m, NightsTotal: 894.50m), price);
    }

    [Fact]
    public void Total_informado_vale_como_desconto_e_guarda_o_calculado()
    {
        var price = PriceOf("2026-10-09", "2026-10-11", informedTotal: 1600m);

        Assert.Equal(1600m, price!.Total);
        Assert.Equal(1789m, price.CalculatedTotal);
    }

    [Fact]
    public void Pacote_em_parte_cobra_a_noite_pelo_preco_do_pacote_dividido()
    {
        var price = PriceOf("2026-10-31", "2026-11-01");

        Assert.Equal(830m, price!.Total); // 2.490 / 3
    }

    [Fact]
    public void Pacote_inteiro_cobra_o_preco_fechado()
    {
        var price = PriceOf("2026-10-30", "2026-11-02");

        Assert.Equal(2490m, price!.Total);
    }

    [Fact]
    public void Sem_preco_cadastrado_e_sem_total_nao_tem_como_gravar()
    {
        var price = PriceOf("2026-10-06", "2026-10-08", data: ScenarioData(weekday: null));

        Assert.Null(price);
    }

    [Fact]
    public void Sem_preco_cadastrado_com_total_informado_vale_o_informado()
    {
        var price = PriceOf("2026-10-06", "2026-10-08", informedTotal: 700m, data: ScenarioData(weekday: null));

        Assert.Equal(new AdminPrice(Total: 700m, CalculatedTotal: 700m, NightsTotal: 700m), price);
    }

    [Fact]
    public void Sem_preco_cadastrado_as_diarias_descontam_o_kit()
    {
        var quote = BookingRules.Quote(D("2026-10-06"), D("2026-10-08"), ScenarioData(weekday: null));

        var price = AdminBookingRules.Price(quote, informedTotal: 700m, kitPrice: 179m);

        Assert.Equal(521m, price!.NightsTotal);
    }
}
