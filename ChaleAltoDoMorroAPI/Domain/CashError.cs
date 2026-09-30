namespace ChaleAltoDoMorroAPI.Domain;

// Motivos para uma operação no caixa ser recusada. Mesmo padrão do StayError:
// a regra só diz O QUE está errado; o serviço (task 15) traduz para o status HTTP.
public abstract record CashError;

public record BookClosed : CashError;                            // 409
public record InvalidAmount : CashError;                         // 400
public record DateOutsideBook(int Year, int Month) : CashError;  // 422
public record CategoryTypeMismatch : CashError;                  // 422
public record CategoryInactive : CashError;                      // 422
