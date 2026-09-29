using System.Linq.Expressions;

namespace ChaleAltoDoMorroAPI.Domain;

// Em que momento a estadia está em relação a "hoje" (filtro "when" do portal).
public enum ReservationWhen
{
    Today,      // check-in ou check-out hoje
    Ongoing,    // checkIn < hoje < checkOut
    Upcoming,   // checkIn > hoje
    Past        // checkOut < hoje
}

// Regras puras de filtro das reservas. Expression (e não Func) para o EF traduzir em WHERE no SQL;
// nos testes, .Compile() transforma em função comum e aplica numa lista em memória.
public static class ReservationFilters
{
    public static Expression<Func<Reservation, bool>> When(ReservationWhen when, DateOnly today) => when switch
    {
        ReservationWhen.Today => r => r.CheckIn == today || r.CheckOut == today,
        ReservationWhen.Ongoing => r => r.CheckIn < today && today < r.CheckOut,
        ReservationWhen.Upcoming => r => r.CheckIn > today,
        ReservationWhen.Past => r => r.CheckOut < today,
        _ => throw new ArgumentOutOfRangeException(nameof(when), when, "Filtro 'when' desconhecido.")
    };
}
