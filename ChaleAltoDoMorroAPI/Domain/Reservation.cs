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
    // Total = o que o hóspede paga. CalculatedTotal = o que o BookingRules calculou na hora.
    // No site são sempre iguais; no portal o admin pode combinar outro valor (desconto, cortesia...).
    public decimal Total { get; set; }
    public decimal CalculatedTotal { get; set; }

    // Só existe na reserva que veio do site (null na criada pelo portal).
    public decimal? EstimatedTotalFromSite { get; set; }

    public ReservationSource Source { get; set; }
    public bool IncludesColonialBreakfast { get; set; }

    public ReservationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public string? Observations { get; set; }

    // Anotação interna do dono (só aparece no portal). Diferente de Observations, que é o que o hóspede escreveu.
    public string? AdminNotes { get; set; }

    // Folga gravada NA CONFIRMAÇÃO (task 16). null = ainda não confirmada.
    // É um snapshot: mudar o global depois não mexe nesta reserva.
    public int? BufferBeforeNights { get; set; }
    public int? BufferAfterNights { get; set; }
    public Guid Guest1Id { get; set; }
    public Guest Guest1 { get; set; } = null!;
    public Guid? Guest2Id { get; set; }
    public Guest? Guest2 { get; set; }

    // Snapshot do contato enviado no formulário. A rota pública nunca sobrescreve o cadastro de um
    // hóspede que já existe (senão quem soubesse o CPF de alguém trocaria o telefone dele).
    public string ContactPhone { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }

    // Origem da pré-reserva (UTM), já normalizada pelo AttributionRules. Colunas simples em vez de tabela à parte:
    // é 1 para 1 com a reserva e facilita o "pré-reservas por origem" no dashboard (GROUP BY attribution_source).
    // AttributionSource null = não sabemos (reserva do portal, site antigo ou navegador sem armazenamento).
    public string? AttributionSource { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }
    public string? UtmContent { get; set; }
    public DateTimeOffset? FirstVisitAt { get; set; }

    // Transições de status: as regras ficam SÓ aqui, e o serviço nunca muda o Status direto.
    //   Pending   → Confirmed
    //   Pending   → Cancelled
    //   Confirmed → Cancelled
    // Qualquer outra (confirmar uma cancelada, cancelar duas vezes...) devolve false e nada muda.

    public bool TryConfirm(DateTimeOffset now)
    {
        if (Status != ReservationStatus.Pending)
            return false;

        Status = ReservationStatus.Confirmed;
        ConfirmedAt = now;
        return true;
    }

    public bool TryCancel(DateTimeOffset now)
    {
        if (Status == ReservationStatus.Cancelled)
            return false;

        Status = ReservationStatus.Cancelled;
        CancelledAt = now;
        return true;
    }
}

// De onde a reserva veio: pré-reserva do site ou cadastro do admin no portal (task 21).
public enum ReservationSource
{
    Site,
    Portal
}

public enum ReservationStatus
{
    Pending,
    Confirmed,
    Cancelled
}
