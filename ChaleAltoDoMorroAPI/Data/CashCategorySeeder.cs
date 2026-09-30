using ChaleAltoDoMorroAPI.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Data;

// Categorias padrão do Livro Caixa (REGRAS_CAIXA.md, regra 19).
// Roda em TODOS os ambientes: sem categoria não há como lançar nada no caixa.
// Só semeia com a tabela vazia: depois disso, quem manda nas categorias é o dono (pelo portal).
public static class CashCategorySeeder
{
    public static async Task SeedAsync(AppDbContext db, ILogger log)
    {
        if (await db.CashCategories.AnyAsync())
            return;

        CashCategory[] categories =
        [
            New("Hospedagem",         CashTransactionType.Income,  "bed",             "#4a7a60", 1),
            New("Kits e extras",      CashTransactionType.Income,  "gift",            "#c9a84c", 2),
            New("Outras entradas",    CashTransactionType.Income,  "circle-plus",     "#2a4436", 3),

            New("Limpeza",            CashTransactionType.Expense, "sparkles",        "#7c9a8a", 1),
            New("Manutenção",         CashTransactionType.Expense, "wrench",          "#a0714f", 2),
            New("Energia",            CashTransactionType.Expense, "zap",             "#e8c97a", 3),
            New("Água",               CashTransactionType.Expense, "droplet",         "#5b8fa8", 4),
            New("Internet",           CashTransactionType.Expense, "wifi",            "#6b7fa8", 5),
            New("Impostos e taxas",   CashTransactionType.Expense, "landmark",        "#8a6d8f", 6),
            New("Produtos e insumos", CashTransactionType.Expense, "shopping-basket", "#b5838d", 7),
            New("Marketing",          CashTransactionType.Expense, "megaphone",       "#c47a5a", 8),
            New("Outras saídas",      CashTransactionType.Expense, "circle-ellipsis", "#7c7268", 9),
        ];

        db.CashCategories.AddRange(categories);
        await db.SaveChangesAsync();

        log.LogInformation("Categorias padrão do Livro Caixa criadas ({Count}).", categories.Length);
    }

    private static CashCategory New(string name, CashTransactionType type, string icon, string color, int order) => new()
    {
        Name = name,
        Type = type,
        Icon = icon,
        Color = color,
        IsActive = true,
        Order = order
    };
}