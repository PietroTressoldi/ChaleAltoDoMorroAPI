namespace ChaleAltoDoMorroAPI.Domain;

// Regras PURAS da reserva criada ou editada pelo admin no portal (task 21).
// O admin pode mais que o site: só a disponibilidade é validada, e o preço pode ser combinado.
// Casos de teste em docs/REGRAS_RESERVA.md, seção "Reserva criada ou editada pelo portal".
public static class AdminBookingRules
{
    /// <summary>
    /// Devolve o primeiro problema da estadia, ou null se ela pode ser gravada.
    /// Diferente do BookingRules.ValidateStay: mínimo de diárias, pacote fechado e datas no passado ficam liberados.
    /// </summary>
    public static StayError? ValidateStay(DateOnly checkIn, DateOnly checkOut, IReadOnlySet<DateOnly> unavailableNights)
    {
        if (checkIn == default || checkOut == default)
            return new InvalidDate();

        if (checkOut <= checkIn)
            return new CheckOutNotAfterCheckIn();

        foreach (var night in BookingRules.Nights(checkIn, checkOut))
        {
            if (unavailableNights.Contains(night))
                return new BlockedNight(night);
        }

        return null;
    }

    /// <summary>
    /// Decide o preço gravado. Com total informado, vale ele; sem, vale o calculado (quote).
    /// Devolve null quando não há como chegar num valor (sem preço cadastrado e sem total informado).
    /// </summary>
    /// <param name="quote">O orçamento do BookingRules.Quote (null quando falta preço cadastrado).</param>
    /// <param name="informedTotal">O total que o admin digitou, ou null para usar o calculado.</param>
    /// <param name="kitPrice">Preço do kit escolhido (0 sem kit). Só é usado quando não há quote.</param>
    public static AdminPrice? Price(StayQuote? quote, decimal? informedTotal, decimal kitPrice)
    {
        if (quote is not null)
            return new AdminPrice(
                Total: informedTotal ?? quote.Total,
                CalculatedTotal: quote.Total,
                NightsTotal: quote.NightsTotal);

        if (informedTotal is not { } total)
            return null;

        // Sem preço cadastrado não existe "calculado": o combinado é a única referência.
        return new AdminPrice(total, CalculatedTotal: total, NightsTotal: Math.Max(total - kitPrice, 0m));
    }
}

/// <param name="Total">O que o hóspede paga.</param>
/// <param name="CalculatedTotal">O que as regras calcularam (para o portal mostrar "preço ajustado").</param>
/// <param name="NightsTotal">Valor das diárias pelo cálculo (sem o kit).</param>
public record AdminPrice(decimal Total, decimal CalculatedTotal, decimal NightsTotal);
