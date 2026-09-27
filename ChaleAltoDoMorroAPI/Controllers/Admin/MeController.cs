using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

// [Authorize] na classe: toda ação nova já nasce protegida.
[ApiController]
[Authorize]
[Route("api/admin/me")]
public class MeController : ControllerBase
{
    [HttpGet]
    public ActionResult<object> GetMe()
    {
        // MapInboundClaims = false (Program.cs): as claims chegam com os nomes curtos do token ("email", "sub").
        return Ok(new { email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value });
    }
}
