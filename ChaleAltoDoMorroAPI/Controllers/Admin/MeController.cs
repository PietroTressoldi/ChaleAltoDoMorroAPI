using System.IdentityModel.Tokens.Jwt;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Global;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

// [Authorize] na classe: toda ação nova já nasce protegida.
[ApiController]
[Authorize]
[EnableCors(CorsPolicies.Portal)]
[Route("api/admin/me")]
public class MeController(UserManager<IdentityUser> userManager, ILogger<MeController> log, SrvRefreshToken srvRefreshToken) : ControllerBase
{
    [HttpGet]
    public ActionResult<object> GetMe()
    {
        // MapInboundClaims = false (Program.cs): as claims chegam com os nomes curtos do token ("email", "sub").
        return Ok(new { email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value });
    }

    [HttpPost("password")]
    [EnableRateLimiting(RateLimitPolicies.Password)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDto request, CancellationToken ct)
    {
        // "sub" = Id do usuário (SrvToken). O Id nunca muda; o e-mail pode mudar um dia.
        var userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (userId is null)
            return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);

        // Token válido, mas o usuário foi apagado depois do login.
        if (user is null)
            return Unauthorized();

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (result.Succeeded)
        {
            // Senha nova derruba todas as sessões lembradas (se alguém roubou um cookie, perde o acesso aqui).
            await srvRefreshToken.RevokeAll(user.Id, ct);
            log.LogInformation("Senha trocada por {UserId}.", user.Id);
            return NoContent();
        }

        // As mensagens já vêm em PT-BR (PtBrIdentityErrorDescriber). "PasswordMismatch" = senha atual errada;
        // os outros códigos são regras da senha nova que falharam.
        var currentPasswordWrong = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch));

        // Senha atual errada aqui não conta para o lockout (só o login conta): o log é o que mostra tentativas repetidas.
        if (currentPasswordWrong)
            log.LogWarning("Troca de senha recusada: senha atual incorreta para {UserId}.", user.Id);

        return Problem(
            title: currentPasswordWrong ? "Senha atual incorreta" : "Senha nova fraca",
            detail: string.Join(" ", result.Errors.Select(e => e.Description)),
            statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}
