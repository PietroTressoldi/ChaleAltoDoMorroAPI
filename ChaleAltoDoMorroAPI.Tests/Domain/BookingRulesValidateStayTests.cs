using ChaleAltoDoMorroAPI.Domain;
using static ChaleAltoDoMorroAPI.Tests.Fixtures.BookingScenario;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Tabelas "Validação — ..." de docs/REGRAS_RESERVA.md (cenário: hoje = quinta 01/10/2026).
public class BookingRulesValidateStayTests
{
    private static StayError? Validate(string checkIn, string checkOut, BookingData? data = null, string? today = null) =>
        BookingRules.ValidateStay(D(checkIn), D(checkOut), data ?? ScenarioData(), today is null ? Today : D(today));

    // ---------- Fim de semana ----------

    [Theory]
    [InlineData("2026-10-09", "2026-10-11")] // sex → dom
    [InlineData("2026-10-10", "2026-10-12")] // sáb → seg
    [InlineData("2026-10-09", "2026-10-12")] // sex → seg
    [InlineData("2026-10-09", "2026-10-10")] // sex → sáb (diária estendida)
    [InlineData("2026-10-10", "2026-10-11")] // sáb → dom (diária estendida)
    [InlineData("2026-10-08", "2026-10-09")] // qui → sex (só a noite de quinta)
    [InlineData("2026-10-08", "2026-10-10")] // qui → sáb (quinta + sexta)
    [InlineData("2026-10-05", "2026-10-06")] // seg → ter
    public void Estadia_valida_de_semana_e_fim_de_semana(string checkIn, string checkOut)
    {
        Assert.Null(Validate(checkIn, checkOut));
    }

    [Theory]
    [InlineData("2026-10-09", "2026-10-10")] // sex → sáb
    [InlineData("2026-10-10", "2026-10-11")] // sáb → dom
    public void Sem_diaria_estendida_fim_de_semana_com_uma_diaria_exige_minimo(string checkIn, string checkOut)
    {
        var error = Assert.IsType<MinNightsNotMet>(Validate(checkIn, checkOut, ScenarioData(extended: null)));
        Assert.Equal(2, error.Required);
        Assert.Equal(1, error.Actual);
    }

    [Theory]
    [InlineData("2026-10-09", "2026-10-10", "2026-10-08")] // sex → sáb: quinta ocupada
    [InlineData("2026-10-09", "2026-10-10", "2026-10-10")] // sex → sáb: sábado ocupado
    [InlineData("2026-10-10", "2026-10-11", "2026-10-09")] // sáb → dom: sexta ocupada
    [InlineData("2026-10-10", "2026-10-11", "2026-10-11")] // sáb → dom: domingo ocupado
    public void Diaria_estendida_exige_as_noites_vizinhas_livres(string checkIn, string checkOut, string blocked)
    {
        var data = ScenarioData() with { BlockedNights = new HashSet<DateOnly> { D(blocked) } };

        var error = Assert.IsType<BlockedNight>(Validate(checkIn, checkOut, data));
        Assert.Equal(D(blocked), error.Night);
    }


    // ---------- Diária estendida com a folga global (task 27) ----------
    // Tabela "Diária estendida com a folga global" de REGRAS_RESERVA.md. "blocked" = noites publicadas.
    [Theory]
    [InlineData("2026-10-10", "2026-10-11", 0, 1, "2026-10-11,2026-10-12,2026-10-13", null)]         // caso 19/12
    [InlineData("2026-10-10", "2026-10-11", 0, 0, "2026-10-11,2026-10-12,2026-10-13", "2026-10-11")] // global 0: como antes
    [InlineData("2026-10-09", "2026-10-10", 1, 0, "2026-10-07,2026-10-08,2026-10-09", "2026-10-09")] // a própria noite
    [InlineData("2026-10-09", "2026-10-10", 1, 0, "2026-10-06,2026-10-07,2026-10-08", null)]         // quinta = folga publicada
    [InlineData("2026-10-09", "2026-10-10", 0, 0, "2026-10-08", "2026-10-08")]                       // global 0: como antes
    public void Estendida_nao_conta_a_folga_global_duas_vezes(
        string checkIn, string checkOut, int bufferBefore, int bufferAfter, string blocked, string? expectedBlocked)
    {
        var data = ScenarioData() with
        {
            BlockedNights = blocked.Split(',').Select(D).ToHashSet(),
            BufferBefore = bufferBefore,
            BufferAfter = bufferAfter,
        };

        var error = Validate(checkIn, checkOut, data);

        if (expectedBlocked is null)
            Assert.Null(error);
        else
            Assert.Equal(D(expectedBlocked), Assert.IsType<BlockedNight>(error).Night);
    }

    [Theory]
    [InlineData("2026-10-11", "2026-10-12")] // dom → seg
    [InlineData("2026-10-11", "2026-10-13")] // dom → ter
    public void Check_in_no_domingo_eh_recusado(string checkIn, string checkOut)
    {
        Assert.IsType<SundayCheckIn>(Validate(checkIn, checkOut));
    }

    [Fact]
    public void Pacote_que_comeca_no_domingo_vale()
    {
        var domingo = new PackageInfo(Guid.Empty, "Domingo", D("2026-10-18"), D("2026-10-20"), 1500m);

        Assert.Null(Validate("2026-10-18", "2026-10-20", ScenarioData() with { Packages = [domingo] }));
    }

    [Fact]
    public void Minimo_de_fim_de_semana_vem_da_regra()
    {
        var error = Assert.IsType<MinNightsNotMet>(
            Validate("2026-10-09", "2026-10-11", ScenarioData(weekendMinNights: 3)));
        Assert.Equal(3, error.Required);
        Assert.Equal(2, error.Actual);
    }

    [Fact]
    public void Sem_regra_de_fim_de_semana_a_sexta_exige_o_minimo_padrao_de_2()
    {
        var error = Assert.IsType<MinNightsNotMet>(
            Validate("2026-10-09", "2026-10-10", ScenarioData(weekend: null, weekendMinNights: null, extended: null)));
        Assert.Equal(2, error.Required);
    }

    [Fact]
    public void Noite_de_terca_exige_uma_diaria()
    {
        Assert.Null(Validate("2026-10-06", "2026-10-07"));
    }

    // ---------- Datas ----------

    [Fact]
    public void Data_nao_informada_eh_invalida()
    {
        // default(DateOnly) = 01/01/0001: é o que chega quando o JSON não traz a data.
        Assert.IsType<InvalidDate>(BookingRules.ValidateStay(default, D("2026-10-07"), ScenarioData(), Today));
        Assert.IsType<InvalidDate>(BookingRules.ValidateStay(D("2026-10-06"), default, ScenarioData(), Today));
    }

    [Fact]
    public void Check_in_no_passado_eh_recusado()
    {
        Assert.IsType<PastCheckIn>(Validate("2026-09-30", "2026-10-02"));
    }

    [Fact]
    public void Check_in_hoje_eh_permitido()
    {
        Assert.Null(Validate("2026-10-01", "2026-10-02"));
    }

    [Theory]
    [InlineData("2026-10-06", "2026-10-06")] // check-out = check-in
    [InlineData("2026-10-06", "2026-10-05")] // check-out antes
    public void Check_out_precisa_ser_depois_do_check_in(string checkIn, string checkOut)
    {
        Assert.IsType<CheckOutNotAfterCheckIn>(Validate(checkIn, checkOut));
    }

    // ---------- Noites bloqueadas ----------

    [Theory]
    [InlineData("2026-10-13", "2026-10-15")] // dorme na noite bloqueada
    [InlineData("2026-10-14", "2026-10-15")] // entra na noite bloqueada
    public void Dormir_em_noite_bloqueada_eh_recusado(string checkIn, string checkOut)
    {
        var error = Assert.IsType<BlockedNight>(Validate(checkIn, checkOut));
        Assert.Equal(D("2026-10-14"), error.Night);
    }

    [Fact]
    public void Sair_no_dia_bloqueado_eh_permitido()
    {
        Assert.Null(Validate("2026-10-13", "2026-10-14"));
    }

    // ---------- Pacote vendido fechado ----------

    [Fact]
    public void Pacote_inteiro_eh_valido()
    {
        Assert.Null(Validate("2026-10-30", "2026-11-02"));
    }

    [Theory]
    [InlineData("2026-10-30", "2026-11-01")] // só parte do pacote
    [InlineData("2026-10-31", "2026-11-02")] // começando no meio
    [InlineData("2026-10-29", "2026-11-02")] // emendando antes
    [InlineData("2026-10-30", "2026-11-03")] // emendando depois
    public void Pacote_parcial_eh_recusado(string checkIn, string checkOut)
    {
        var error = Assert.IsType<PartialPackage>(Validate(checkIn, checkOut));
        Assert.Equal(Finados, error.Package);
    }

    [Fact]
    public void Estadia_que_toca_dois_pacotes_aponta_o_primeiro_pela_ordem_das_noites()
    {
        // Mesmo com a lista vindo "fora de ordem" do banco, vale a primeira noite de pacote (igual ao site).
        var data = ScenarioData() with { Packages = [Indisponivel, Finados] };

        var error = Assert.IsType<PartialPackage>(Validate("2026-10-29", "2026-11-14", data));
        Assert.Equal(Finados, error.Package);
    }

    [Theory]
    [InlineData("2026-10-28", "2026-10-30")] // sai no dia em que o pacote começa
    [InlineData("2026-11-02", "2026-11-03")] // entra no dia em que o pacote termina
    public void Estadia_encostada_no_pacote_eh_valida(string checkIn, string checkOut)
    {
        Assert.Null(Validate(checkIn, checkOut));
    }

    [Fact]
    public void Pacote_com_noite_bloqueada_nunca_pode_ser_reservado()
    {
        var error = Assert.IsType<BlockedNight>(Validate("2026-11-13", "2026-11-16"));
        Assert.Equal(D("2026-11-14"), error.Night);
    }

    [Fact]
    public void Pacote_que_ja_comecou_eh_recusado()
    {
        Assert.IsType<PastCheckIn>(Validate("2026-10-30", "2026-11-02", today: "2026-10-31"));
    }

    // ---------- Meses abertos (task 28) ----------
    // Tabela "Meses abertos" de REGRAS_RESERVA.md, com OpenUntil = 31/12/2026.
    private static readonly PackageInfo AnoNovo = new(Guid.Empty, "Ano Novo", D("2026-12-31"), D("2027-01-03"), 2990m);
    private static readonly PackageInfo Reis = new(Guid.Empty, "Reis", D("2027-01-08"), D("2027-01-10"), 1800m);

    private static BookingData OpenUntilDecember(params PackageInfo[] packages) =>
        ScenarioData() with { OpenUntil = D("2026-12-31"), Packages = packages };

    [Theory]
    [InlineData("2026-12-30", "2027-01-01", null)]         // última noite 31/12: o check-out pode cair em janeiro
    [InlineData("2026-12-31", "2027-01-02", "2027-01-01")]
    [InlineData("2027-01-05", "2027-01-07", "2027-01-05")]
    public void Estadia_comum_so_ate_o_mes_aberto(string checkIn, string checkOut, string? closedNight)
    {
        var error = Validate(checkIn, checkOut, OpenUntilDecember());

        if (closedNight is null)
            Assert.Null(error);
        else
            Assert.Equal(D(closedNight), Assert.IsType<MonthNotOpen>(error).Night);
    }

    [Fact]
    public void Pacote_vale_pelo_mes_do_check_in()
    {
        var data = OpenUntilDecember(AnoNovo, Reis);

        Assert.Null(Validate("2026-12-31", "2027-01-03", data));
        Assert.Equal(D("2027-01-08"), Assert.IsType<MonthNotOpen>(Validate("2027-01-08", "2027-01-10", data)).Night);
    }

    [Fact]
    public void Sem_mes_aberto_nao_tem_limite()
    {
        Assert.Null(Validate("2027-01-05", "2027-01-07"));
    }

    [Theory]
    [InlineData("2027-01-15", "2027-01-31")]
    [InlineData("2026-12-01", "2026-12-31")]
    [InlineData("2028-02-10", "2028-02-29")] // ano bissexto
    public void Mes_aberto_vai_ate_o_ultimo_dia(string date, string expected)
    {
        Assert.Equal(D(expected), BookingRules.EndOfMonth(D(date)));
    }

}
