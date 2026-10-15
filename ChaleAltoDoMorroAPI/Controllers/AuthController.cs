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
public class AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, SrvRefreshToken srvRefreshToken, ILogger<AuthController> log) : ControllerBase
{

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
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
        var session = await srvRefreshToken.Start(user, request.RememberMe, ct);
        WriteRefreshCookie(session);
        return Ok(session.Login);
    }

    // Só o portal manda esse header. Um formulário de outro site não consegue mandar header customizado
    // sem o preflight do CORS, e o preflight barra quem não é o portal: defesa contra CSRF além do SameSite.
    private const string PortalHeaderValue = "chale-portal";
    private const string RefreshCookie = "chale_refresh";

    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken ct)
    {
        if (!FromPortal())
            return Problem("Sessão expirada.", statusCode: 401);

        var session = await srvRefreshToken.Rotate(Request.Cookies[RefreshCookie], ct);
        if (session is null)
        {
            DeleteRefreshCookie();
            return Problem("Sessão expirada.", statusCode: 401);
        }

        WriteRefreshCookie(session);
        return Ok(session.Login);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (!FromPortal())
            return Problem("Sessão expirada.", statusCode: 401);

        await srvRefreshToken.Revoke(Request.Cookies[RefreshCookie], ct);
        DeleteRefreshCookie();
        return NoContent();
    }

    private bool FromPortal() => Request.Headers.XRequestedWith == PortalHeaderValue;

    private void WriteRefreshCookie(RefreshResult session) =>
        Response.Cookies.Append(RefreshCookie, session.Token, RefreshCookieOptions(session.Persistent ? session.ExpiresAt : null));

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookie, RefreshCookieOptions(expires: null));

    // HttpOnly: o JavaScript não lê. Secure: só HTTPS (localhost conta como seguro).
    // SameSite=Strict: o navegador só manda o cookie em pedidos do MESMO site (gestao. e api. são o mesmo site).
    // Path=/api/auth: o cookie nem viaja nas outras rotas. Sem Expires = cookie de sessão (some ao fechar o navegador).
    private static CookieOptions RefreshCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires,
    };
}
