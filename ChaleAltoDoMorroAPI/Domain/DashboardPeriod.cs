namespace ChaleAltoDoMorroAPI.Domain;

// Tipo de período do dashboard (task 19). Na query string chega como texto: ?period=quarter.
public enum PeriodKind
{
    Month,
    Quarter,
    Semester,
    Year
}
// Período do dashboard (task 19): from/to inclusivos (como no contrato), o nome para a tela e o período anterior.
public record DashboardPeriod(PeriodKind Kind, int Year, int Index, DateOnly From, DateOnly To, string Label)
{
    private static readonly string[] MonthNames =
    [
        "janeiro", "fevereiro", "março", "abril", "maio", "junho",
        "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
    ];

    // Quantos meses cada tipo tem.
    public static int MonthsIn(PeriodKind kind) => kind switch
    {
        PeriodKind.Month => 1,
        PeriodKind.Quarter => 3,
        PeriodKind.Semester => 6,
        _ => 12
    };

    // Quantos períodos desse tipo cabem num ano: 12 meses, 4 trimestres, 2 semestres, 1 ano.
    private static int PerYear(PeriodKind kind) => 12 / MonthsIn(kind);

    // null = ano ou index fora da faixa (o controller transforma em 400).
    public static DashboardPeriod? Of(PeriodKind kind, int year, int index)
    {
        if (year is < 2000 or > 2100) return null;

        if (kind == PeriodKind.Year) index = 1;  // index é ignorado no "year"
        else if (index < 1 || index > PerYear(kind)) return null;

        return Build(kind, year, index);
    }

    // Numera os períodos em sequência (ano * perYear + posição), volta 1 e desmonta de novo em ano e index.
    // Assim a virada de ano (T1 → T4 do ano anterior, jan → dez) sai da conta, sem if.
    public DashboardPeriod Previous()
    {
        var perYear = PerYear(Kind);
        var seq = Year * perYear + (Index - 1) - 1;
        return Build(Kind, seq / perYear, seq % perYear + 1);
    }

    // Sem validação: só é chamado com valores já conferidos (por Of ou pela conta de Previous).
    private static DashboardPeriod Build(PeriodKind kind, int year, int index)
    {
        var months = MonthsIn(kind);
        var from = new DateOnly(year, (index - 1) * months + 1, 1);
        var to = from.AddMonths(months).AddDays(-1);  // último dia, inclusive (fevereiro e bissexto saem certos)
        return new DashboardPeriod(kind, year, index, from, to, LabelOf(kind, year, index));
    }

    private static string LabelOf(PeriodKind kind, int year, int index) => kind switch
    {
        PeriodKind.Month => $"{MonthNames[index - 1]} de {year}",
        PeriodKind.Quarter => $"{index}º trimestre de {year}",
        PeriodKind.Semester => $"{index}º semestre de {year}",
        _ => $"{year}"
    };
}
