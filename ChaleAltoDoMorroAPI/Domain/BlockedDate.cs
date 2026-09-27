namespace ChaleAltoDoMorroAPI.Domain;

public class BlockedDate
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public string? Reason { get; set; }
}
