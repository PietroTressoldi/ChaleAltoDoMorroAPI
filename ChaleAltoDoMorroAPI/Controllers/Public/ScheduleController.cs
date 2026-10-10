using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Public;

// Horários de check-in e check-out (task 24). Só horários: a folga e o resto das configurações não saem aqui.
[Route("api/schedule")]
[ApiController]
public class ScheduleController(SrvSettings srvSettings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ScheduleSettingsDto>> GetSchedule(CancellationToken ct) =>
        Ok(await srvSettings.GetSchedule(ct));
}
