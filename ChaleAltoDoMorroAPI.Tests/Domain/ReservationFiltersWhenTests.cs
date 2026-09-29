using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Filtro "when" do portal (task 9), com HOJE fixo em qui 01/10/2026 (BookingScenario.Today).
public class ReservationFiltersWhenTests
{
    private static bool Matches(ReservationWhen when, string checkIn, string checkOut)
    {
        var reservation = new Reservation { CheckIn = D(checkIn), CheckOut = D(checkOut) };
        return ReservationFilters.When(when, Today).Compile()(reservation);
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-03", true)]  // check-in hoje
    [InlineData("2026-09-29", "2026-10-01", true)]  // check-out hoje
    [InlineData("2026-09-30", "2026-10-02", false)] // em andamento, mas nada acontece hoje
    [InlineData("2026-10-02", "2026-10-04", false)] // amanhã
    public void Today_e_check_in_ou_check_out_hoje(string checkIn, string checkOut, bool expected)
    {
        Assert.Equal(expected, Matches(ReservationWhen.Today, checkIn, checkOut));
    }

    [Theory]
    [InlineData("2026-09-30", "2026-10-02", true)]  // hóspede está no chalé
    [InlineData("2026-10-01", "2026-10-03", false)] // chega hoje: é "today", ainda não "ongoing"
    [InlineData("2026-09-29", "2026-10-01", false)] // sai hoje: é "today", não "ongoing"
    [InlineData("2026-09-20", "2026-09-22", false)] // já acabou
    public void Ongoing_e_hoje_estritamente_dentro_da_estadia(string checkIn, string checkOut, bool expected)
    {
        Assert.Equal(expected, Matches(ReservationWhen.Ongoing, checkIn, checkOut));
    }

    [Theory]
    [InlineData("2026-10-02", "2026-10-04", true)]  // amanhã
    [InlineData("2026-10-01", "2026-10-03", false)] // hoje não é "próxima"
    [InlineData("2026-09-30", "2026-10-02", false)]
    public void Upcoming_e_check_in_depois_de_hoje(string checkIn, string checkOut, bool expected)
    {
        Assert.Equal(expected, Matches(ReservationWhen.Upcoming, checkIn, checkOut));
    }

    [Theory]
    [InlineData("2026-09-28", "2026-09-30", true)]  // saiu ontem
    [InlineData("2026-09-29", "2026-10-01", false)] // sai hoje: ainda é "today"
    [InlineData("2026-10-05", "2026-10-07", false)]
    public void Past_e_check_out_antes_de_hoje(string checkIn, string checkOut, bool expected)
    {
        Assert.Equal(expected, Matches(ReservationWhen.Past, checkIn, checkOut));
    }
}
