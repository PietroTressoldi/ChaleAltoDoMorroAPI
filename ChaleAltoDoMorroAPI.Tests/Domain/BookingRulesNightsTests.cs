using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Expansão "reserva → noites" (task 6): intervalo meio-aberto [checkIn, checkOut).
// É o que o GET /api/blocked-dates e a validação da pré-reserva usam para as reservas confirmadas.
public class BookingRulesNightsTests
{
    private static IReadOnlyList<DateOnly> Nights(string checkIn, string checkOut) =>
        BookingRules.Nights(D(checkIn), D(checkOut));

    [Fact]
    public void Uma_diaria_ocupa_so_a_noite_do_check_in()
    {
        Assert.Equal([D("2026-10-10")], Nights("2026-10-10", "2026-10-11"));
    }

    [Fact]
    public void Varias_diarias_ocupam_cada_noite_em_ordem()
    {
        Assert.Equal(
            [D("2026-10-09"), D("2026-10-10"), D("2026-10-11")],
            Nights("2026-10-09", "2026-10-12"));
    }

    // Se um dia o portal ganhar "check-out bloqueia a data" (docs/TASKS.md, ideias para o futuro),
    // este teste deve quebrar: é o lembrete de que a regra mudou de propósito.
    [Fact]
    public void Dia_do_check_out_nao_fica_ocupado()
    {
        Assert.DoesNotContain(D("2026-10-12"), Nights("2026-10-10", "2026-10-12"));
    }

    [Fact]
    public void Estadia_que_atravessa_o_ano_continua_em_ordem()
    {
        Assert.Equal(
            [D("2026-12-30"), D("2026-12-31"), D("2027-01-01")],
            Nights("2026-12-30", "2027-01-02"));
    }

    [Theory]
    [InlineData("2026-10-10", "2026-10-10")] // check-out no mesmo dia
    [InlineData("2026-10-12", "2026-10-10")] // check-out antes do check-in
    public void Estadia_sem_noites_nao_ocupa_nada(string checkIn, string checkOut)
    {
        Assert.Empty(Nights(checkIn, checkOut));
    }
}
