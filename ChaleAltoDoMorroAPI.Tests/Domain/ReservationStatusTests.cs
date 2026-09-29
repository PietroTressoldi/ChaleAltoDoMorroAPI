using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Transições de status da reserva (task 9): Pending → Confirmed, Pending/Confirmed → Cancelled.
public class ReservationStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 14, 30, 0, TimeSpan.FromHours(-3));

    private static Reservation WithStatus(ReservationStatus status) => new() { Status = status };

    [Fact]
    public void Confirmar_pendente_muda_o_status_e_grava_a_data()
    {
        var reservation = WithStatus(ReservationStatus.Pending);

        Assert.True(reservation.TryConfirm(Now));
        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(Now, reservation.ConfirmedAt);
    }

    [Theory]
    [InlineData(ReservationStatus.Confirmed)] // confirmar duas vezes
    [InlineData(ReservationStatus.Cancelled)] // cancelada não volta
    public void Confirmar_o_que_nao_esta_pendente_nao_muda_nada(ReservationStatus status)
    {
        var reservation = WithStatus(status);

        Assert.False(reservation.TryConfirm(Now));
        Assert.Equal(status, reservation.Status);
        Assert.Null(reservation.ConfirmedAt);
    }

    [Theory]
    [InlineData(ReservationStatus.Pending)]
    [InlineData(ReservationStatus.Confirmed)]
    public void Cancelar_pendente_ou_confirmada_muda_o_status_e_grava_a_data(ReservationStatus status)
    {
        var reservation = WithStatus(status);

        Assert.True(reservation.TryCancel(Now));
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(Now, reservation.CancelledAt);
    }

    [Fact]
    public void Cancelar_duas_vezes_nao_muda_a_data_do_primeiro_cancelamento()
    {
        var firstCancel = Now.AddDays(-1);
        var reservation = new Reservation { Status = ReservationStatus.Cancelled, CancelledAt = firstCancel };

        Assert.False(reservation.TryCancel(Now));
        Assert.Equal(firstCancel, reservation.CancelledAt);
    }
}
