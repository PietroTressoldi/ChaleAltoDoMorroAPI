namespace ChaleAltoDoMorroAPI.Domain;

// Um refresh token emitido (task 29). Família = todos os tokens que nasceram do MESMO login, um rotacionando o outro.
public class RefreshToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty; // IdentityUser.Id
    public string TokenHash { get; set; } = string.Empty; // SHA-256 do token; o token puro só existe no cookie
    public Guid FamilyId { get; set; }
    public bool Persistent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } // prazo absoluto da família
    public DateTimeOffset? RevokedAt { get; set; }  // rotacionado, logout, reuso ou troca de senha
    public Guid? ReplacedById { get; set; } // o token que nasceu dele na rotação
}
