namespace ChaleAltoDoMorroAPI.Domain;

// Motivos para uma estadia ser recusada. Cada erro carrega só os dados que fazem sentido para ele.
// Ordem de verificação em docs/REGRAS_RESERVA.md ("Validação da estadia").
public abstract record StayError;

public record InvalidDate : StayError;

public record PastCheckIn : StayError;

public record CheckOutNotAfterCheckIn : StayError;

public record BlockedNight(DateOnly Night) : StayError;

public record PartialPackage(PackageInfo Package) : StayError;

// O mínimo só passa de 1 quando a estadia tem noite de sexta ou sábado: este erro sempre é "fim de semana".
public record MinNightsNotMet(int Required, int Actual) : StayError;
