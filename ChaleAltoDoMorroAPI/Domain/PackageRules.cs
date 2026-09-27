namespace ChaleAltoDoMorroAPI.Domain;

// Regras puras de pacote especial (sem EF): usadas pelo admin ao criar/editar pacotes.
public static class PackageRules
{
    /// <summary>
    /// Dois intervalos de noites [checkIn, checkOut) se cruzam?
    /// Um pacote que começa no dia do check-out de outro NÃO se sobrepõe (o check-out não é noite).
    /// </summary>
    public static bool Overlaps(DateOnly checkInA, DateOnly checkOutA, DateOnly checkInB, DateOnly checkOutB) =>
        checkInA < checkOutB && checkInB < checkOutA;
}
