using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Controllers.Public;

[Route("api/packages")]
[ApiController]
public class SpecialPackageController(AppDbContext db, IClock clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SpecialPackageDto>>> GetUpcomingPackages(CancellationToken ct)
    {
        var today = clock.Today();

        // checkOut depois de hoje: o pacote ainda tem pelo menos uma noite por vir (ou em andamento).
        var packages = await db.SpecialPackages
            .AsNoTracking()
            .Where(p => p.CheckOut > today)
            .OrderBy(p => p.CheckIn)
            .Select(p => new SpecialPackageDto(p.Id, p.Name, p.Description, p.CheckIn, p.CheckOut, p.Price))
            .ToListAsync(ct);

        return Ok(packages);
    }
}
