namespace ChaleAltoDoMorroAPI.Domain;

// Modelo de despesa fixa (luz, internet...). Aplicar no mês gera uma CashTransaction independente.
public class RecurringExpense
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int CategoryId { get; set; }
    public int DayOfMonth { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public bool IsActive { get; set; }

    // Guardados como o dia 1 do mês (2026-10-01). Na API, saem como "2026-10".
    public DateOnly StartMonth { get; set; }
    public DateOnly? EndMonth { get; set; }
}