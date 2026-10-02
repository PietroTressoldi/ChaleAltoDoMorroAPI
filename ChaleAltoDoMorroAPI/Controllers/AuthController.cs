using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Global;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ChaleAltoDoMorroAPI.Controllers;

// Login é do portal: mesma política de CORS das rotas admin.
[ApiController]
[EnableCors(CorsPolicies.Portal)]
[Route("api/auth")]
public class AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, SrvToken srvToken, ILogger<AuthController> log) : ControllerBase
{

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            // Sem o e-mail digitado no log: ele é dado pessoal (e às vezes é a senha colada no campo errado).
            log.LogWarning("Login recusado: e-mail não cadastrado.");
            return Problem("E-mail ou senha inválidos!", statusCode: 401);
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        // Conta bloqueada (IsLockedOut) cai aqui de propósito: avisar "bloqueada" confirmaria que o e-mail existe.
        // A resposta é a mesma para todos; só o log (que só nós vemos) separa os casos.
        if (!result.Succeeded)
        {
            // O Id do usuário (Guid) identifica a conta sem expor quem é a pessoa.
            if (result.IsLockedOut)
                log.LogWarning("Login recusado: conta {UserId} bloqueada.", user.Id);
            else
                log.LogWarning("Login recusado: senha incorreta para {UserId}.", user.Id);

            return Problem("E-mail ou senha inválidos!", statusCode: 401);
        }

        log.LogInformation("Login de {UserId}.", user.Id);
        var loginResponse = srvToken.CreateToken(user);
        return Ok(loginResponse);
    }

}
