namespace ChaleAltoDoMorroAPI.Domain;

public class CashTransaction
{

    public Guid Id { get; set; }
    public int CashBookId { get; set; }
    public DateOnly Date { get; set; }
    public CashTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public CashCategory Category { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }

    // Preenchido só quando a transação veio de "aplicar despesas fixas".
    public int? RecurringExpenseId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

}
public enum CashTransactionType
{
    Income,
    Expense
}

public enum PaymentMethod
{
    Pix,
    Cash,
    Card,
    Transfer,
    Other
}