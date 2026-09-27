namespace ChaleAltoDoMorroAPI.Domain;

// Catálogo de kits românticos (o que o site lista em /api/extras e o portal edita).
public class Kit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public List<string> Items { get; set; } = [];
    public bool IsActive { get; set; }
    public int Order { get; set; }
}
