namespace ChaleAltoDoMorroAPI.Domain;

public class CashBook
{

    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public CashBookStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public List<CashTransaction> Transactions { get; set; } = [];

    // Transições: Open → Closed e Closed → Open. Fechar um fechado ou reabrir um aberto devolve false (regra 5).
    public bool TryClose(DateTimeOffset now)
    {
        if (Status == CashBookStatus.Closed)
            return false;

        Status = CashBookStatus.Closed;
        ClosedAt = now;
        UpdatedAt = now;
        return true;
    }

    public bool TryReopen(DateTimeOffset now)
    {
        if (Status == CashBookStatus.Open)
            return false;

        Status = CashBookStatus.Open;
        ClosedAt = null;
        UpdatedAt = now;
        return true;
    }
}
public enum CashBookStatus
{
    Open,
    Closed
}