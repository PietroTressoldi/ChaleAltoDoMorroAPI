namespace ChaleAltoDoMorroAPI.Domain;

// Estadia + folga efetiva dela (Before = "check-in bloqueia a véspera", After = "check-out bloqueia a data").
public record BufferedStay(DateOnly CheckIn, DateOnly CheckOut, int Before, int After);

// Folga entre hóspedes (task 16): o que uma reserva OCUPA, que é diferente do que ela COBRA (BookingRules.Nights).
// Regras PURAS (sem EF, sem relógio); casos em docs/REGRAS_RESERVA.md, seção "Folga entre hóspedes".
public static class OccupancyRules
{
    /// <summary>
    /// Regra 4: noites que uma reserva CONFIRMADA publica como indisponíveis, com os globais atuais.
    /// [in − max(B, gA), out + max(A, gB)): a folga da própria reserva E a que a estadia nova vai precisar.
    /// Assim o calendário do site, que só conhece noites bloqueadas, impede exatamente o que a regra 3 proíbe.
    /// </summary>
    public static IReadOnlyList<DateOnly> PublishedNights(BufferedStay confirmed, int globalBefore, int globalAfter)
    {
        var from = confirmed.CheckIn.AddDays(-Math.Max(confirmed.Before, globalAfter));
        var to = confirmed.CheckOut.AddDays(Math.Max(confirmed.After, globalBefore));
        return BookingRules.Nights(from, to);
    }

    /// <summary>
    /// Noites de folga da PRÓPRIA reserva (o snapshot, sem os globais): o que o calendário do portal pinta como "folga".
    /// Antes: [in − B, in). Depois: [out, out + A). Diferente de PublishedNights, que também soma a folga global.
    /// </summary>
    public static IReadOnlyList<DateOnly> BufferNights(BufferedStay stay) =>
    [
        .. BookingRules.Nights(stay.CheckIn.AddDays(-stay.Before), stay.CheckIn),
        .. BookingRules.Nights(stay.CheckOut, stay.CheckOut.AddDays(stay.After))
    ];

    /// <summary>
    /// Regra 3: S e R não convivem quando uma invade a área protegida da outra ([in − B, out + A)).
    /// As folgas podem se sobrepor entre si: as duas são "casa vazia".
    /// </summary>
    public static bool Conflicts(BufferedStay s, BufferedStay r) =>
        PackageRules.Overlaps(s.CheckIn.AddDays(-s.Before), s.CheckOut.AddDays(s.After), r.CheckIn, r.CheckOut)
        || PackageRules.Overlaps(r.CheckIn.AddDays(-r.Before), r.CheckOut.AddDays(r.After), s.CheckIn, s.CheckOut);

    /// <summary>
    /// O que impede CONFIRMAR a estadia (com a folga efetiva dela), ou null se pode confirmar.
    /// Regra 5: bloqueio manual só conta nas noites COBRADAS; a folga pode cair sobre ele.
    /// </summary>
    public static StayError? ValidateConfirmation(BufferedStay stay, IReadOnlySet<DateOnly> manualBlocks, IEnumerable<BufferedStay> confirmed)
    {
        foreach (var night in BookingRules.Nights(stay.CheckIn, stay.CheckOut))
        {
            if (manualBlocks.Contains(night))
                return new BlockedNight(night);
        }

        var other = confirmed.OrderBy(r => r.CheckIn).FirstOrDefault(r => Conflicts(stay, r));
        return other is null ? null : new ReservationConflict(other.CheckIn, other.CheckOut);
    }
}
