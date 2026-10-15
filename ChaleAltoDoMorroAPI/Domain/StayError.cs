namespace ChaleAltoDoMorroAPI.Domain;

// Motivos para uma estadia ser recusada. Cada erro carrega só os dados que fazem sentido para ele.
// Ordem de verificação em docs/REGRAS_RESERVA.md ("Validação da estadia").
public abstract record StayError;

public record InvalidDate : StayError;

public record PastCheckIn : StayError;

// O site não aceita check-in no domingo (o admin pode).
public record SundayCheckIn : StayError;

public record CheckOutNotAfterCheckIn : StayError;

public record BlockedNight(DateOnly Night) : StayError;

public record PartialPackage(PackageInfo Package) : StayError;

// O mínimo só passa de 1 quando a estadia tem noite de sexta, sábado ou domingo: este erro sempre é "fim de semana".
public record MinNightsNotMet(int Required, int Actual) : StayError;

// Confirmação com folga (task 16): a estadia invade a área protegida de outra reserva confirmada (ou vice-versa).
public record ReservationConflict(DateOnly CheckIn, DateOnly CheckOut) : StayError;

// Task 28: o site só aceita reservas até o mês aberto no portal. Night = primeira noite fora dele.
public record MonthNotOpen(DateOnly Night) : StayError;