using System.ComponentModel.DataAnnotations;

namespace ChaleAltoDoMorroAPI.Dtos;

public record LoginRequest([Required][EmailAddress] string Email, [Required] string Password);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
