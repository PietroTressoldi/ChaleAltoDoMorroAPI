namespace ChaleAltoDoMorroAPI.Domain;

public class Reservation
{
    public Guid Id { get; set; }
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int Nights { get; set; }

    // Snapshot do pacote no momento da reserva.
    public Guid? PackageId { get; set; }
    public string? PackageName { get; set; }

    // Snapshot do kit (um por reserva) no momento da reserva.
    public Guid? KitId { get; set; }
    public string? KitName { get; set; }
    public decimal? KitPrice { get; set; }

    public decimal NightsTotal { get; set; }
    public decimal Total { get; set; }
    public decimal EstimatedTotalFromSite { get; set; }
    public bool IncludesColonialBreakfast { get; set; }

    public ReservationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public string? Observations { get; set; }

    public Guid Guest1Id { get; set; }
    public Guest Guest1 { get; set; } = null!;
    public Guid? Guest2Id { get; set; }
    public Guest? Guest2 { get; set; }

    // Snapshot do contato enviado no formulário. A rota pública nunca sobrescreve o cadastro de um
    // hóspede que já existe (senão quem soubesse o CPF de alguém trocaria o telefone dele).
    public string ContactPhone { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
}

public enum ReservationStatus
{
    Pending,
    Confirmed,
    Cancelled
}
