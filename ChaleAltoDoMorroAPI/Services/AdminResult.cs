namespace ChaleAltoDoMorroAPI.Services;

// Resultado dos serviços do admin. O serviço diz O QUE aconteceu; o controller traduz para HTTP
// (mesma ideia do PreReservationResponse, só que genérica porque os casos se repetem em todo CRUD).
public abstract record AdminResult<T>;

public record Success<T>(T Value) : AdminResult<T>;
public record NotFound<T>(string Message) : AdminResult<T>;
public record Conflict<T>(string Message) : AdminResult<T>;   // 409: choca com o estado atual (sobreposição, noite ocupada)
public record Invalid<T>(string Message) : AdminResult<T>;    // 422: regra de negócio entre campos (ex.: check-out antes do check-in)
