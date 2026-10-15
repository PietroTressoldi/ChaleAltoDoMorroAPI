namespace ChaleAltoDoMorroAPI.Domain;

// Regras de reserva PURAS: sem EF, sem HTTP, sem relógio ("hoje" chega por parâmetro).
// Espelham landing/src/lib/booking.ts; casos de teste em docs/REGRAS_RESERVA.md.
public static class BookingRules
{
    public const int DefaultWeekendMinNights = 2;

    /// <summary>A partir de quantas diárias a cesta de café colonial está inclusa (entregue na chegada).</summary>
    public const int ColonialBreakfastMinNights = 2;

    /// <summary>A partir de quantas diárias a noite de semana tem desconto (WeekdayLongStayPrice).</summary>
    public const int LongStayMinNights = 2;

    /// <summary>Devolve o primeiro problema da estadia, ou null se ela é válida.</summary>
    public static StayError? ValidateStay(DateOnly checkIn, DateOnly checkOut, BookingData data, DateOnly today)
    {
        if (checkIn == default || checkOut == default)
            return new InvalidDate();

        if (checkIn < today)
            return new PastCheckIn();

        if (checkOut <= checkIn)
            return new CheckOutNotAfterCheckIn();

        var nights = Nights(checkIn, checkOut);

        foreach (var night in nights)
        {
            if (data.BlockedNights.Contains(night))
                return new BlockedNight(night);
        }

        // Pacote é vendido fechado: se a estadia toca uma noite dele, tem que ser exatamente o pacote.
        // Percorre as noites em ordem (não a lista de pacotes) para apontar o mesmo pacote que o site.
        foreach (var night in nights)
        {
            var package = PackageOn(night, data.Packages);
            if (package is null)
                continue;

            var isExactPackage = package.CheckIn == checkIn && package.CheckOut == checkOut;
            if (!isExactPackage)
                return new PartialPackage(package);

            // Pacote vale pelo mês do CHECK-IN (o Ano Novo entra em dezembro e sai em janeiro).
            return package.CheckIn > data.OpenUntil ? new MonthNotOpen(package.CheckIn) : null;
        }

        var closedNight = nights.FirstOrDefault(night => night > data.OpenUntil);
        if (closedNight != default)
            return new MonthNotOpen(closedNight);

        if (checkIn.DayOfWeek == DayOfWeek.Sunday)
            return new SundayCheckIn();

        // Diária estendida: entra 10h30 e sai 16h, então a véspera e a noite do check-out precisam estar livres.
        // Só confere o lado com folga global 0: com 1+, essa noite já vem nas bloqueadas (task 27, bug do 19/12).
        if (IsExtendedStay(checkIn, checkOut) && data.ExtendedPrice is not null)
        {
            if (data.BufferBefore == 0 && data.BlockedNights.Contains(checkIn.AddDays(-1)))
                return new BlockedNight(checkIn.AddDays(-1));

            if (data.BufferAfter == 0 && data.BlockedNights.Contains(checkOut))
                return new BlockedNight(checkOut);

            return null;
        }

        var required = nights.Any(IsWeekendNight)
            ? data.WeekendMinNights ?? DefaultWeekendMinNights
            : 1;

        return nights.Count < required ? new MinNightsNotMet(required, nights.Count) : null;
    }

    /// <summary>Noites da estadia: do check-in até a véspera do check-out (a noite do check-out não conta).</summary>
    public static IReadOnlyList<DateOnly> Nights(DateOnly checkIn, DateOnly checkOut)
    {
        var nights = new List<DateOnly>();
        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
            nights.Add(night);

        return nights;
    }

    /// <summary>Diária estendida: 1 noite com entrada na sexta ou no sábado.</summary>
    public static bool IsExtendedStay(DateOnly checkIn, DateOnly checkOut) =>
        checkOut == checkIn.AddDays(1) && checkIn.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;

    public static bool IsWeekendNight(DateOnly night) =>
        night.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday;


    /// <summary>Último dia do mês da data. O mês aberto é sempre guardado assim (2027-01-15 → 2027-01-31).</summary>
    public static DateOnly EndOfMonth(DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));


    /// <summary>Pacote que contém a NOITE desta data (entrada &lt;= noite &lt; saída), se houver.</summary>
    public static PackageInfo? PackageOn(DateOnly night, IReadOnlyList<PackageInfo> packages) =>
        packages.FirstOrDefault(p => p.CheckIn <= night && night < p.CheckOut);

    /// <summary>Preço de UMA noite (pacote &gt; fim de semana &gt; semana), ou null se não há preço cadastrado.</summary>
    /// <param name="longStay">A estadia tem 2+ diárias: a noite de semana usa o WeekdayLongStayPrice.</param>
    public static NightPrice? PriceNight(DateOnly night, BookingData data, bool longStay = false)
    {
        var package = PackageOn(night, data.Packages);
        if (package is not null)
        {
            var packageNights = Nights(package.CheckIn, package.CheckOut).Count;
            return new NightPrice(night, NightKind.Package, Money(package.Price / packageNights), package.Id);
        }

        // Sem preço de fim de semana cadastrado, sexta, sábado e domingo caem no preço de semana.
        if (IsWeekendNight(night) && data.WeekendPrice is { } weekendPrice)
            return new NightPrice(night, NightKind.Weekend, weekendPrice);

        // Sem diária de semana não se orça, nem com o desconto cadastrado (ele mora na mesma regra).
        if (data.WeekdayPrice is not { } weekdayPrice)
            return null;

        var price = longStay && data.WeekdayLongStayPrice is { } longStayPrice ? longStayPrice : weekdayPrice;
        return new NightPrice(night, NightKind.Weekday, price);
    }

    /// <summary>Orçamento de uma estadia VÁLIDA (chame ValidateStay antes). Null se faltar preço no cadastro.</summary>
    public static StayQuote? Quote(DateOnly checkIn, DateOnly checkOut, BookingData data, KitInfo? kit = null)
    {
        var stayNights = Nights(checkIn, checkOut);
        var longStay = stayNights.Count >= LongStayMinNights;

        // Estendida só fora de pacote: se a noite é de pacote, vale o preço do pacote.
        var extendedPrice = IsExtendedStay(checkIn, checkOut) && PackageOn(checkIn, data.Packages) is null
            ? data.ExtendedPrice
            : null;

        var nights = new List<NightPrice>();
        foreach (var night in stayNights)
        {
            var price = extendedPrice is { } extended
                ? new NightPrice(night, NightKind.Extended, extended)
                : PriceNight(night, data, longStay);
            if (price is null)
                return null; // cadastro incompleto: não inventa valor

            nights.Add(price);
        }

        var package = data.Packages.FirstOrDefault(p => p.CheckIn == checkIn && p.CheckOut == checkOut);

        IReadOnlyList<QuoteLine> lines = package is not null
            ? [new QuoteLine(NightKind.Package, package.Name, nights.Count, UnitPrice: null, package.Price)]
            : nights
                .GroupBy(n => (n.Kind, n.Price)) // GroupBy mantém a ordem da primeira ocorrência
                .Select(g => new QuoteLine(g.Key.Kind, Detail: null, g.Count(), g.Key.Price, g.Key.Price * g.Count()))
                .ToList();

        var nightsTotal = lines.Sum(l => l.Subtotal);
        var kitTotal = kit?.Price ?? 0m;

        return new StayQuote(
            checkIn,
            checkOut,
            nights,
            lines,
            nightsTotal,
            kit,
            kitTotal,
            Total: nightsTotal + kitTotal,
            package,
            IncludesColonialBreakfast: nights.Count >= ColonialBreakfastMinNights);
    }

    // decimal não tem o problema de 0,1 + 0,2 do JavaScript; o arredondamento só aparece em divisões
    // (preço de referência da noite de pacote). AwayFromZero = mesmo resultado do Math.round do site.
    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
