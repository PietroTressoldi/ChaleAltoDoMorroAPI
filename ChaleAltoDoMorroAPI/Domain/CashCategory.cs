namespace ChaleAltoDoMorroAPI.Domain;

public class CashCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CashTransactionType Type { get; set; }
    public string Color { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int Order { get; set; }
}


