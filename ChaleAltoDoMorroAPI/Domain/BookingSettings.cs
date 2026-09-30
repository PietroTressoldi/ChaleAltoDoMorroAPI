
namespace ChaleAltoDoMorroAPI.Domain;

// Configurações de reserva que o portal edita. Tabela de UMA linha só (Id sempre 1).
public class BookingSettings
{
    public const int SingletonId = 1;
    public const int MaxBufferNights = 3;

    public int Id { get; set; } = SingletonId;

    // "Check-in bloqueia a véspera": noites livres ANTES de cada reserva.
    public int BufferBeforeNights { get; set; }

    // "Check-out bloqueia a data": noites livres DEPOIS de cada reserva.
    public int BufferAfterNights { get; set; }
}

