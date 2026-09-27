using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, SrvToken srvToken) : ControllerBase
{

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Problem("E-mail ou senha inválidos!", statusCode: 401);

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        // Conta bloqueada (IsLockedOut) cai aqui de propósito: avisar "bloqueada" confirmaria que o e-mail existe.
        if (!result.Succeeded)
            return Problem("E-mail ou senha inválidos!", statusCode: 401);

        var loginResponse = srvToken.CreateToken(user);
        return Ok(loginResponse);
    }

}
