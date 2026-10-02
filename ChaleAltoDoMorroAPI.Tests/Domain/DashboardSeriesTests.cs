using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Série do gráfico e agenda do dashboard (task 19): fatias do período, valores por fatia e próximos check-ins/outs.
public class DashboardSeriesTests
{
    private static readonly DashboardPeriod October = DashboardPeriod.Of(PeriodKind.Month, 2026, 10)!;
    private static readonly DashboardPeriod Q4 = DashboardPeriod.Of(PeriodKind.Quarter, 2026, 4)!;

    private static readonly HashSet<DateOnly> NoBlocks = [];

    private static StayTotal Stay(string checkIn, string checkOut, decimal total) => new(D(checkIn), D(checkOut), total);

    // ---------- Buckets ----------

    [Fact]
    public void Buckets_do_mes_tem_um_por_dia()
    {
        var buckets = DashboardRules.Buckets(October);

        Assert.Equal(31, buckets.Count);
        Assert.Equal(new SeriesBucket("01/10", D("2026-10-01"), D("2026-10-01")), buckets[0]);
        Assert.Equal(new SeriesBucket("31/10", D("2026-10-31"), D("2026-10-31")), buckets[^1]);
    }

    [Fact]
    public void Buckets_do_trimestre_tem_um_por_mes()
    {
        var buckets = DashboardRules.Buckets(Q4);

        Assert.Equal(new[]
        {
            new SeriesBucket("out", D("2026-10-01"), D("2026-10-31")),
            new SeriesBucket("nov", D("2026-11-01"), D("2026-11-30")),
            new SeriesBucket("dez", D("2026-12-01"), D("2026-12-31"))
        }, buckets.ToArray());
    }

    [Fact]
    public void Buckets_do_ano_vao_de_jan_a_dez()
    {
        var buckets = DashboardRules.Buckets(DashboardPeriod.Of(PeriodKind.Year, 2026, 1)!);

        Assert.Equal(12, buckets.Count);
        Assert.Equal("jan", buckets[0].Label);
        Assert.Equal(D("2026-02-28"), buckets[1].To);
        Assert.Equal("dez", buckets[^1].Label);
    }

    // ---------- Series ----------

    [Fact]
    public void Series_faturamento_vai_inteiro_para_o_mes_do_check_in_e_as_noites_sao_recortadas()
    {
        var series = DashboardRules.Series(DashboardRules.Buckets(Q4), [Stay("2026-10-30", "2026-11-02", 1789m)], [], NoBlocks);

        Assert.Equal(1789m, series[0].ReservationRevenue);
        Assert.Equal(0m, series[1].ReservationRevenue);
        Assert.Equal(0.065m, series[0].OccupancyRate); // 2 / 31
        Assert.Equal(0.033m, series[1].OccupancyRate); // 1 / 30
        Assert.Equal(0m, series[2].OccupancyRate);
    }

    [Fact]
    public void Series_estadia_que_comeca_antes_do_periodo_conta_noites_mas_nao_faturamento()
    {
        var series = DashboardRules.Series(DashboardRules.Buckets(Q4), [Stay("2026-09-29", "2026-10-02", 2000m)], [], NoBlocks);

        Assert.All(series, p => Assert.Equal(0m, p.ReservationRevenue));
        Assert.Equal(0.032m, series[0].OccupancyRate); // 1 / 31
    }

    [Fact]
    public void Series_caixa_por_fatia_com_liquido_negativo()
    {
        CashEntry[] cash =
        [
            new(D("2026-10-05"), CashTransactionType.Income, 500m),
            new(D("2026-10-10"), CashTransactionType.Expense, 250m),
            new(D("2026-11-10"), CashTransactionType.Expense, 100m)
        ];

        var series = DashboardRules.Series(DashboardRules.Buckets(Q4), [], cash, NoBlocks);

        Assert.Equal((500m, 250m, 250m), (series[0].CashIncome, series[0].CashExpense, series[0].CashNet));
        Assert.Equal((0m, 100m, -100m), (series[1].CashIncome, series[1].CashExpense, series[1].CashNet));
        Assert.Equal((0m, 0m, 0m), (series[2].CashIncome, series[2].CashExpense, series[2].CashNet));
    }

    [Fact]
    public void Series_por_dia_ocupacao_e_0_ou_1_e_dia_bloqueado_vale_0()
    {
        var series = DashboardRules.Series(
            DashboardRules.Buckets(October), [Stay("2026-10-09", "2026-10-11", 1580m)], [], new HashSet<DateOnly> { D("2026-10-20") });

        Assert.Equal(1m, series[8].OccupancyRate);   // noite de 09/10
        Assert.Equal(1m, series[9].OccupancyRate);   // noite de 10/10
        Assert.Equal(0m, series[10].OccupancyRate);  // 11/10: dia do check-out, noite livre
        Assert.Equal(0m, series[19].OccupancyRate);  // 20/10 bloqueado: sem divisão por zero
        Assert.Equal(1580m, series[8].ReservationRevenue);
        Assert.All(series, p => Assert.InRange(p.OccupancyRate, 0m, 1m));
    }

    // ---------- Agenda ----------

    private static AgendaStay Agenda(string name, string checkIn, string checkOut) =>
        new(Guid.NewGuid(), name, D(checkIn), D(checkOut));

    [Fact]
    public void Agenda_mistura_check_ins_e_check_outs_a_partir_de_hoje()
    {
        // HOJE = qui 01/10/2026 (BookingScenario.Today).
        AgendaStay[] stays =
        [
            Agenda("Ana", "2026-09-29", "2026-10-02"),   // já está no chalé: só o check-out entra
            Agenda("Bruno", "2026-10-02", "2026-10-04"),
            Agenda("Carla", "2026-10-09", "2026-10-11")
        ];

        var events = DashboardRules.Agenda(stays, Today, 10);

        Assert.Equal(new[]
        {
            ("Ana", "2026-10-02", AgendaType.CheckOut),  // no mesmo dia, quem sai (12h) vem antes de quem chega (15h)
            ("Bruno", "2026-10-02", AgendaType.CheckIn),
            ("Bruno", "2026-10-04", AgendaType.CheckOut),
            ("Carla", "2026-10-09", AgendaType.CheckIn),
            ("Carla", "2026-10-11", AgendaType.CheckOut)
        }, events.Select(e => (e.GuestName, $"{e.Date:yyyy-MM-dd}", e.Type)).ToArray());
    }

    [Fact]
    public void Agenda_inclui_o_que_acontece_hoje()
    {
        var events = DashboardRules.Agenda([Agenda("Ana", "2026-10-01", "2026-10-03")], Today, 10);

        Assert.Equal(D("2026-10-01"), events[0].Date);
        Assert.Equal(AgendaType.CheckIn, events[0].Type);
    }

    [Fact]
    public void Agenda_respeita_o_limite()
    {
        var stays = Enumerable.Range(0, 6)
            .Select(i => new AgendaStay(Guid.NewGuid(), $"Hóspede {i}", Today.AddDays(i * 3 + 1), Today.AddDays(i * 3 + 3)))
            .ToList();

        var events = DashboardRules.Agenda(stays, Today, 10);

        Assert.Equal(10, events.Count);
        Assert.Equal(events.OrderBy(e => e.Date).Select(e => e.Date), events.Select(e => e.Date));
    }
}
