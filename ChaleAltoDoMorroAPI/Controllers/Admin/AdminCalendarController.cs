using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

// Agenda e mini calendário do portal. Intervalo inválido (sem datas, to < from, > 100 dias) nem chega aqui: 400 automático.
[Route("api/admin/calendar")]
public class AdminCalendarController(SrvCalendar srvCalendar) : AdminControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CalendarDto>> Get([FromQuery] CalendarQuery query, CancellationToken ct) =>
        Ok(await srvCalendar.Get(query.From!.Value, query.To!.Value, ct));
}