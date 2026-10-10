using System.Globalization;
using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Tests.Fixtures;

// Cenário de referência de docs/REGRAS_RESERVA.md, usado por todos os testes de BookingRules.
//   out/2026: qui 01 (HOJE) · sex 09 · sáb 10 · dom 11 · qua 14 (bloqueada) · sáb 24 (bloqueada)
//             sex 30 → seg 02/11 = pacote "Finados" (3 diárias, R$ 2.490)
//   nov/2026: sex 13 → seg 16 = pacote "Indisponível" (sáb 14 bloqueado)
public static class BookingScenario
{
    public static readonly DateOnly Today = D("2026-10-01");

    // Ids fixos: se um teste falhar, a mensagem é a mesma em toda execução.
    public static readonly PackageInfo Finados =
        new(Guid.Parse("00000000-0000-0000-0000-00000000f1a0"), "Finados", D("2026-10-30"), D("2026-11-02"), 2490m);

    public static readonly PackageInfo Indisponivel =
        new(Guid.Parse("00000000-0000-0000-0000-0000000001d0"), "Indisponível", D("2026-11-13"), D("2026-11-16"), 2000m);

    public static readonly KitInfo ToqueDeRomance =
        new(Guid.Parse("00000000-0000-0000-0000-00000000c170"), "Kit Toque de Romance", 179m);

    // Patch 1 (task 23): diária de semana 645 em estadia de 2+ diárias e diária estendida 1.190.
    public static BookingData ScenarioData(
        decimal? weekday = 790m, decimal? weekend = 894.50m, int? weekendMinNights = 2,
        decimal? longStay = 645m, decimal? extended = 1190m) =>
        new(
            WeekdayPrice: weekday,
            WeekendPrice: weekend,
            WeekendMinNights: weekendMinNights,
            BlockedNights: new HashSet<DateOnly> { D("2026-10-14"), D("2026-10-24"), D("2026-11-14") },
            Packages: [Finados, Indisponivel],
            WeekdayLongStayPrice: longStay,
            ExtendedPrice: extended);

    // [InlineData] só aceita constantes, então as datas chegam como texto "AAAA-MM-DD".
    // ParseExact + InvariantCulture: não depende da cultura da máquina que roda os testes.
    public static DateOnly D(string date) => DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
