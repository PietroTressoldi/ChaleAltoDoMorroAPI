namespace ChaleAltoDoMorroAPI.Domain
{
    public abstract record PreReservationResponse;

    public record Created(Guid id, decimal Total) : PreReservationResponse;
    public record StayRejected(StayError error) : PreReservationResponse;
    public record KitNotFound() : PreReservationResponse;
    public record PackageMismatch(DateOnly CheckIn, DateOnly CheckOut, PackageInfo? package) : PreReservationResponse;

    // Sem preço de semana cadastrado não dá para orçar (BookingRules.Quote devolve null): problema de cadastro, não do visitante.
    public record PricingNotConfigured() : PreReservationResponse;


}
