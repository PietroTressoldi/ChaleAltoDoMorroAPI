using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

// Dashboard do portal. period/year/index inválidos nem chegam aqui: 400 automático (DashboardQuery.Validate).
[Route("api/admin/dashboard")]
public class AdminDashboardController(SrvDashboard srvDashboard) : AdminControllerBase
{

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardDto>> Get([FromQuery] DashboardQuery query, CancellationToken ct) =>
        Ok(await srvDashboard.Get(query.ToPeriod(), ct));

}
