using System.ComponentModel.DataAnnotations;

namespace ChaleAltoDoMorroAPI.Dtos.Admin;

// Contratos do portal (rotas /api/admin/..., autenticadas). Validações que dependem do banco ficam nos serviços.

// ---------- Preços ----------

// Sempre por DIÁRIA. "R$ 1.789 pelas 2 diárias" é conta de tela: o portal divide e manda 894,50.
public record PutPricingRuleRequest(
    [Required, StringLength(100)] string Label,
    [Range(typeof(decimal), "0.01", "99999", ParseLimitsInInvariantCulture = true, ErrorMessage = "O preço por diária precisa ser maior que zero.")] decimal PricePerNight,
    [Range(1, 7, ErrorMessage = "O mínimo de diárias fica entre 1 e 7.")] int? MinNights,
    [Range(typeof(decimal), "0.01", "99999", ParseLimitsInInvariantCulture = true, ErrorMessage = "A diária de 2+ noites precisa ser maior que zero.")] decimal? LongStayPricePerNight = null);

// ---------- Pacotes ----------

public record SavePackageRequest(
    [Required, StringLength(120)] string Name,
    [Required, StringLength(1000)] string Description,
    DateOnly CheckIn,
    DateOnly CheckOut,
    [Range(typeof(decimal), "0.01", "999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "O preço do pacote precisa ser maior que zero.")] decimal Price);

// PendingReservationsInDates: aviso (não impede). O dono decide o que fazer com quem já pediu essas datas.
public record AdminPackageDto(
    Guid Id, string Name, string Description, DateOnly CheckIn, DateOnly CheckOut, decimal Price,
    int PendingReservationsInDates);

// ---------- Kits ----------

public record SaveKitRequest(
    [Required, StringLength(120)] string Name,
    [StringLength(500)] string? Description,
    [Range(typeof(decimal), "0.01", "99999", ParseLimitsInInvariantCulture = true, ErrorMessage = "O preço do kit precisa ser maior que zero.")] decimal Price,
    [Required] List<string> Items,
    bool IsActive,
    [Range(0, 1000)] int Order);

public record SetKitActiveRequest(bool IsActive);

public record AdminKitDto(Guid Id, string Name, string? Description, decimal Price, List<string> Items, bool IsActive, int Order);

// ---------- Bloqueios manuais ----------

// From e To são NOITES, ambas inclusas: de 10 a 12 bloqueia as noites 10, 11 e 12.
public record CreateBlockedRangeRequest(
    DateOnly From,
    DateOnly To,
    [StringLength(200)] string? Reason);

// Aqui o reason pode sair: a rota é autenticada (na pública ele nunca sai).
public record AdminBlockedDateDto(DateOnly Date, string? Reason);

public record BlockedRangeCreatedDto(int Created, int AlreadyBlocked);


// ---------- Configurações ----------

// Folga entre hóspedes (task 16). "Check-in bloqueia a véspera" = Before; "Check-out bloqueia a data" = After.
public record BufferSettingsDto(
    [Range(0, 3, ErrorMessage = "A folga vai de 0 a 3 noites.")] int BufferBeforeNights,
    [Range(0, 3, ErrorMessage = "A folga vai de 0 a 3 noites.")] int BufferAfterNights);

// Horários (task 24). Só informativos: o site mostra "check-in a partir das 15h".
public record ScheduleSettingsDto(TimeOnly CheckInTime, TimeOnly CheckOutTime, TimeOnly ExtendedCheckInTime, TimeOnly ExtendedCheckOutTime);