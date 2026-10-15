using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Public;

// Mês aberto e folga global (tasks 27 e 28): o site valida o calendário com as mesmas regras da API.
// Nada sensível: são configurações, sem dado de hóspede.
[Route("api/booking-settings")]
[ApiController]
public class BookingSettingsController(SrvSettings srvSettings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PublicBookingSettingsDto>> Get(CancellationToken ct) =>
        Ok(await srvSettings.GetPublicBookingSettings(ct));
}
