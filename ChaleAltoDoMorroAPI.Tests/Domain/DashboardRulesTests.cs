using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Contas do dashboard (task 19): recorte de noites ao período, noites disponíveis, ocupação e ticket médio.
public class DashboardRulesTests
{
    private const string OctFrom = "2026-10-01", OctTo = "2026-10-31";
    private const string NovFrom = "2026-11-01", NovTo = "2026-11-30";

    [Theory]
    [InlineData("2026-10-30", "2026-11-02", OctFrom, OctTo, 2)]  // critério de aceite: 30 e 31 em outubro
    [InlineData("2026-10-30", "2026-11-02", NovFrom, NovTo, 1)]  // critério de aceite: 1 em novembro
    [InlineData("2026-10-09", "2026-10-11", OctFrom, OctTo, 2)]  // inteira dentro
    [InlineData("2026-09-29", "2026-10-02", OctFrom, OctTo, 1)]  // começa antes do período
    [InlineData("2026-09-29", "2026-10-01", OctFrom, OctTo, 0)]  // check-out no 1º dia: a noite é de setembro
    [InlineData("2026-10-31", "2026-11-02", OctFrom, OctTo, 1)]  // check-in no último dia
    [InlineData("2026-09-30", "2026-11-02", OctFrom, OctTo, 31)] // cobre o mês inteiro
    [InlineData("2026-09-20", "2026-09-22", OctFrom, OctTo, 0)]  // antes
    [InlineData("2026-11-05", "2026-11-07", OctFrom, OctTo, 0)]  // depois
    public void NightsWithin_recorta_a_estadia_ao_periodo(string checkIn, string checkOut, string from, string to, int expected)
    {
        Assert.Equal(expected, DashboardRules.NightsWithin(D(checkIn), D(checkOut), D(from), D(to)));
    }

    [Fact]
    public void NightsWithin_somado_nos_meses_da_o_total_da_estadia()
    {
        var oct = DashboardRules.NightsWithin(D("2026-10-30"), D("2026-11-02"), D(OctFrom), D(OctTo));
        var nov = DashboardRules.NightsWithin(D("2026-10-30"), D("2026-11-02"), D(NovFrom), D(NovTo));

        Assert.Equal(3, oct + nov);
    }

    [Theory]
    [InlineData(OctFrom, OctTo, 0, 31)]
    [InlineData(OctFrom, OctTo, 3, 28)]
    [InlineData(OctFrom, OctTo, 40, 0)]             // nunca negativo
    [InlineData("2026-10-01", "2026-12-31", 0, 92)] // 4º trimestre
    public void AvailableNights_desconta_bloqueios_manuais(string from, string to, int blocked, int expected)
    {
        Assert.Equal(expected, DashboardRules.AvailableNights(D(from), D(to), blocked));
    }

    [Theory]
    [InlineData(0, 0, 0.0)]     // sem noites disponíveis: 0, sem divisão por zero
    [InlineData(5, 0, 0.0)]
    [InlineData(0, 31, 0.0)]
    [InlineData(16, 90, 0.178)] // exemplo do contrato
    [InlineData(31, 31, 1.0)]
    [InlineData(40, 31, 1.0)]   // vendido > disponível (bloqueio criado depois): limita a 1
    public void OccupancyRate_fica_entre_0_e_1(int nightsSold, int available, double expected)
    {
        var rate = DashboardRules.OccupancyRate(nightsSold, available);

        Assert.Equal((decimal)expected, rate);
        Assert.InRange(rate, 0m, 1m);
    }

    [Theory]
    [InlineData(15230, 7, 2175.71)] // exemplo do contrato
    [InlineData(0, 0, 0.0)]
    [InlineData(1000, 0, 0.0)]      // sem reservas: 0, sem divisão por zero
    public void AverageTicket_divide_pelo_numero_de_reservas(double revenue, int reservations, double expected)
    {
        Assert.Equal((decimal)expected, DashboardRules.AverageTicket((decimal)revenue, reservations));
    }
}
