using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Public;

[Route("/health")]
[ApiController]
public class HealthController : ControllerBase
{

    [HttpGet]
    public ActionResult GetHealth()
    {
        var statusResponse = new { status = "ok"};
        return Ok(statusResponse);
    }

}
