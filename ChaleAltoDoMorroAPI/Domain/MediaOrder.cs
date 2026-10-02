namespace ChaleAltoDoMorroAPI.Domain;

public static class MediaOrder
{
    // O portal manda a lista inteira na nova ordem: os mesmos ids do banco, cada um uma vez.
    public static bool IsCompleteList(IReadOnlyCollection<Guid> existing, IReadOnlyList<Guid> requested) =>
        requested.Count == existing.Count && requested.ToHashSet().SetEquals(existing);
}