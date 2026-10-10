using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// docs/REGRAS_RESERVA.md, tabela "Folga entre hóspedes" (hoje = 01/10/2026).
// Reserva confirmada R: 09/10 → 11/10 (noites 09 e 10).
public class OccupancyRulesTests
{
    private static BufferedStay R(int before, int after) => new(D("2026-10-09"), D("2026-10-11"), before, after);

    private static string[] Published(BufferedStay r, int globalBefore, int globalAfter) =>
        OccupancyRules.PublishedNights(r, globalBefore, globalAfter).Select(n => n.ToString("dd")).ToArray();

    // A estadia nova passa pela MESMA validação do site, com as noites publicadas por R como bloqueadas.
    private static StayError? ValidateNewStay(BufferedStay r, int globalBefore, int globalAfter, string checkIn, string checkOut)
    {
        var data = ScenarioData() with
        {
            BlockedNights = OccupancyRules.PublishedNights(r, globalBefore, globalAfter).ToHashSet()
        };
        return BookingRules.ValidateStay(D(checkIn), D(checkOut), data, Today);
    }

    // ---------- Noites publicadas (regra 4) ----------

    [Theory]
    [InlineData(0, 0, 0, 0, new[] { "09", "10" })]             // sem folga: comportamento de antes
    [InlineData(0, 1, 0, 1, new[] { "08", "09", "10", "11" })] // check-out bloqueia a data
    [InlineData(1, 0, 1, 0, new[] { "08", "09", "10", "11" })] // check-in bloqueia a véspera
    [InlineData(0, 1, 0, 0, new[] { "09", "10", "11" })]       // global mudou para 0/0: vale o snapshot de R
    [InlineData(0, 0, 0, 2, new[] { "07", "08", "09", "10" })] // global passou para 0/2 depois de R
    public void Noites_publicadas_por_R(int before, int after, int globalBefore, int globalAfter, string[] expected)
    {
        Assert.Equal(expected, Published(R(before, after), globalBefore, globalAfter));
    }

    // ---------- Estadia nova contra as noites publicadas ----------

    [Theory]
    // "Entra no dia em que R sai" (11/10) virou domingo sem check-in no site (task 23): o encaixe está em Conflito_entre_duas_estadias.
    [InlineData(0, 0, 0, 0, "2026-10-07", "2026-10-09")] // sai no dia em que R entra
    [InlineData(0, 1, 0, 1, "2026-10-12", "2026-10-13")]
    [InlineData(0, 1, 0, 1, "2026-10-07", "2026-10-08")]
    [InlineData(1, 0, 1, 0, "2026-10-12", "2026-10-13")]
    public void Estadia_nova_que_respeita_a_folga_passa(int before, int after, int globalBefore, int globalAfter, string checkIn, string checkOut)
    {
        Assert.Null(ValidateNewStay(R(before, after), globalBefore, globalAfter, checkIn, checkOut));
    }

    [Theory]
    [InlineData(0, 1, 0, 1, "2026-10-11", "2026-10-12", "2026-10-11")] // check-out bloqueia a data
    [InlineData(0, 1, 0, 1, "2026-10-07", "2026-10-09", "2026-10-08")] // a folga depois da NOVA cairia na noite 09 de R
    [InlineData(1, 0, 1, 0, "2026-10-07", "2026-10-09", "2026-10-08")] // check-in bloqueia a véspera
    [InlineData(0, 1, 0, 0, "2026-10-11", "2026-10-12", "2026-10-11")] // global mudou para 0/0: vale o snapshot de R
    [InlineData(0, 0, 0, 2, "2026-10-07", "2026-10-08", "2026-10-07")] // a folga de 2 noites da nova invadiria R
    public void Estadia_nova_que_invade_a_folga_e_recusada(int before, int after, int globalBefore, int globalAfter,
        string checkIn, string checkOut, string blocked)
    {
        var error = Assert.IsType<BlockedNight>(ValidateNewStay(R(before, after), globalBefore, globalAfter, checkIn, checkOut));
        Assert.Equal(D(blocked), error.Night);
    }

    // ---------- Conflito entre duas estadias (regra 3) ----------

    [Theory]
    [InlineData(0, 0, "2026-10-11", "2026-10-12", 0, 0, false)] // encostadas, sem folga
    [InlineData(0, 1, "2026-10-11", "2026-10-12", 0, 1, true)]  // S entra na folga depois de R
    [InlineData(0, 0, "2026-10-11", "2026-10-12", 1, 0, true)]  // a véspera de S cai na noite 10 de R
    [InlineData(0, 1, "2026-10-12", "2026-10-13", 0, 1, false)] // folga respeitada
    [InlineData(0, 1, "2026-10-07", "2026-10-08", 0, 1, false)] // as folgas se sobrepõem (as duas são casa vazia)
    public void Conflito_entre_duas_estadias(int rBefore, int rAfter, string sIn, string sOut, int sBefore, int sAfter, bool expected)
    {
        var s = new BufferedStay(D(sIn), D(sOut), sBefore, sAfter);

        Assert.Equal(expected, OccupancyRules.Conflicts(s, R(rBefore, rAfter)));
        Assert.Equal(expected, OccupancyRules.Conflicts(R(rBefore, rAfter), s)); // simétrica
    }

    // ---------- Confirmação (task 9 + regra 5) ----------

    [Fact]
    public void Confirmar_pendente_dentro_da_folga_de_R_e_recusado()
    {
        // Global 0/1: a pendente P 11/10 → 12/10 invade a folga de R (0/1).
        var p = new BufferedStay(D("2026-10-11"), D("2026-10-12"), 0, 1);

        var error = OccupancyRules.ValidateConfirmation(p, new HashSet<DateOnly>(), [R(0, 1)]);

        var conflict = Assert.IsType<ReservationConflict>(error);
        Assert.Equal(D("2026-10-09"), conflict.CheckIn);
    }

    [Fact]
    public void Confirmar_pendente_quando_R_foi_confirmada_sem_folga_passa()
    {
        var p = new BufferedStay(D("2026-10-11"), D("2026-10-12"), 0, 1);

        Assert.Null(OccupancyRules.ValidateConfirmation(p, new HashSet<DateOnly>(), [R(0, 0)]));
    }

    [Fact]
    public void Folga_pode_cair_sobre_bloqueio_manual()
    {
        // Regra 5: confirmar R (0/1) com bloqueio manual em 11/10 → ok, o bloqueio só conta nas noites cobradas.
        var error = OccupancyRules.ValidateConfirmation(R(0, 1), new HashSet<DateOnly> { D("2026-10-11") }, []);

        Assert.Null(error);
    }

    [Fact]
    public void Bloqueio_manual_numa_noite_cobrada_impede_confirmar()
    {
        var error = OccupancyRules.ValidateConfirmation(R(0, 1), new HashSet<DateOnly> { D("2026-10-10") }, []);

        Assert.Equal(D("2026-10-10"), Assert.IsType<BlockedNight>(error).Night);
    }

    [Fact]
    public void Folga_nao_entra_no_preco()
    {
        // R com folga 0/1 continua 2 diárias, R$ 1.789: o que se cobra é BookingRules.Nights, não a ocupação.
        var quote = BookingRules.Quote(D("2026-10-09"), D("2026-10-11"), ScenarioData(), kit: null);

        Assert.Equal(2, BookingRules.Nights(D("2026-10-09"), D("2026-10-11")).Count);
        Assert.Equal(1789m, quote!.Total);
    }

    // ---------- Diária estendida: as noites vizinhas ficam livres (regra 8, task 23) ----------

    private static readonly BufferedStay Estendida = new(D("2026-10-10"), D("2026-10-11"), 0, 0);

    [Fact]
    public void Diaria_estendida_publica_as_noites_vizinhas()
    {
        Assert.Equal(["09", "10", "11"], Published(Estendida, 0, 0));
    }

    [Fact]
    public void Diaria_estendida_mostra_as_noites_vizinhas_como_folga()
    {
        Assert.Equal(["09", "11"], OccupancyRules.BufferNights(Estendida).Select(n => n.ToString("dd")).ToArray());
    }

    [Theory]
    [InlineData("2026-10-08", "2026-10-10", true)]  // qui → sáb sai no sábado: bate com a entrada das 10h30
    [InlineData("2026-10-11", "2026-10-13", true)]  // dom → ter (admin) entra no domingo: bate com a saída das 16h
    [InlineData("2026-10-08", "2026-10-09", false)] // qui → sex: a sexta fica livre
    [InlineData("2026-10-12", "2026-10-14", false)] // seg → qua: o domingo fica livre
    public void Diaria_estendida_conflita_com_quem_encosta(string sIn, string sOut, bool expected)
    {
        var s = new BufferedStay(D(sIn), D(sOut), 0, 0);

        Assert.Equal(expected, OccupancyRules.Conflicts(s, Estendida));
        Assert.Equal(expected, OccupancyRules.Conflicts(Estendida, s));
    }

    [Fact]
    public void Folga_maior_que_1_na_estendida_e_mantida()
    {
        var estendida = Estendida with { Before = 2 };

        Assert.Equal(["08", "09", "11"], OccupancyRules.BufferNights(estendida).Select(n => n.ToString("dd")).ToArray());
    }

    // ---------- Noites de folga da própria reserva (calendário do portal, task 18) ----------

    [Theory]
    [InlineData(0, 0, new string[0])]              // sem folga
    [InlineData(0, 1, new[] { "11" })]             // caso do CONTRATO_PORTAL: check-out bloqueia a data
    [InlineData(1, 1, new[] { "08", "11" })]       // véspera + dia do check-out
    [InlineData(2, 0, new[] { "07", "08" })]       // duas noites antes, nada depois
    public void Noites_de_folga_de_R(int before, int after, string[] expected)
    {
        Assert.Equal(expected, OccupancyRules.BufferNights(R(before, after)).Select(n => n.ToString("dd")).ToArray());
    }
}
