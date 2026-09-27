using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Global;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ChaleAltoDoMorroAPI.Services;

public class SrvToken(IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider)
{

    public LoginResponse CreateToken(IdentityUser user)
    {
        List<Claim> claims = new()
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            // Login é por e-mail (FindByEmailAsync), então quem chega aqui sempre tem e-mail.
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var jwt = jwtOptions.Value;

        var keyBytes = Encoding.UTF8.GetBytes(jwt.Key);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var expiresAt = timeProvider.GetUtcNow().AddMinutes(jwt.ExpiresMinutes);
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, null, expiresAt.UtcDateTime, credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponse(tokenString, expiresAt);
    }

}
