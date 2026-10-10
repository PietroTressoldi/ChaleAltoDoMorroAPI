using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

[Route("api/admin/settings")]
public class AdminSettingsController(SrvSettings srvSettings) : AdminControllerBase
{
    [HttpGet("buffer")]
    public async Task<ActionResult<BufferSettingsDto>> GetBuffer(CancellationToken ct) =>
        Ok(await srvSettings.GetBuffer(ct));

    [HttpPut("buffer")]
    public async Task<ActionResult<BufferSettingsDto>> PutBuffer(BufferSettingsDto request, CancellationToken ct) =>
        Ok(await srvSettings.PutBuffer(request, ct));

    [HttpGet("schedule")]
    public async Task<ActionResult<ScheduleSettingsDto>> GetSchedule(CancellationToken ct) =>
        Ok(await srvSettings.GetSchedule(ct));

    [HttpPut("schedule")]
    public async Task<ActionResult<ScheduleSettingsDto>> PutSchedule(ScheduleSettingsDto request, CancellationToken ct) =>
        Ok(await srvSettings.PutSchedule(request, ct));
}