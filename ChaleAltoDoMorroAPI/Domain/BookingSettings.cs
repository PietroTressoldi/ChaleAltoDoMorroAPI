
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

    // Meses abertos (task 28): ÚLTIMO dia do mês aberto para o site. null = sem limite.
    public DateOnly? BookingOpenUntil { get; set; }

    // Horários (só informativos: aparecem no site e no portal, não mudam regra nenhuma).
    public TimeOnly CheckInTime { get; set; } = DefaultCheckInTime;
    public TimeOnly CheckOutTime { get; set; } = DefaultCheckOutTime;
    public TimeOnly ExtendedCheckInTime { get; set; } = DefaultExtendedCheckInTime;
    public TimeOnly ExtendedCheckOutTime { get; set; } = DefaultExtendedCheckOutTime;



    public static readonly TimeOnly DefaultCheckInTime = new(15, 0);
    public static readonly TimeOnly DefaultCheckOutTime = new(12, 0);
    public static readonly TimeOnly DefaultExtendedCheckInTime = new(10, 30);
    public static readonly TimeOnly DefaultExtendedCheckOutTime = new(16, 0);
}

