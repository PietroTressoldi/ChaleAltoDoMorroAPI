using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Interfaces;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Controllers;

[Route("api/blocked-dates")]
[ApiController]
public class BlockedDateController(SrvAvailability srvAvailability) : ControllerBase
{
    // O "reason" do banco não sai daqui: a rota é pública e o motivo pode ter anotação interna.
    [HttpGet]
    public async Task<ActionResult<List<BlockedDateDto>>> GetUpcomingBlockedDates(CancellationToken ct = default)
    {
        var unavailableNights = await srvAvailability.GetUnavailableNights(ct);
        var response = unavailableNights.Select(n => new BlockedDateDto(n)).ToList();

        return Ok(response);
    }
}
