using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Controllers;

[Route("api/blocked-dates")]
[ApiController]
public class BlockedDateController(AppDbContext db, IClock clock) : ControllerBase
{
    // O "reason" do banco não sai daqui: a rota é pública e o motivo pode ter anotação interna.
    [HttpGet]
    public async Task<ActionResult<List<BlockedDateDto>>> GetUpcomingBlockedDates(CancellationToken ct)
    {
        var today = clock.Today();

        var blockedDates = await db.BlockedDates
            .AsNoTracking()
            .Where(b => b.Date >= today)
            .OrderBy(b => b.Date)
            .Select(b => new BlockedDateDto(b.Date))
            .ToListAsync(ct);

        return Ok(blockedDates);
    }
}
