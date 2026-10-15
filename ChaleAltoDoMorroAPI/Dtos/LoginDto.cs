using System.ComponentModel.DataAnnotations;

namespace ChaleAltoDoMorroAPI.Dtos;

public record LoginRequest([Required][EmailAddress] string Email, [Required] string Password, bool RememberMe = false);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
