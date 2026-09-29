using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Controllers.Public;

// A rota é /api/extras porque é o nome que o site usa (contrato); no domínio a entidade é Kit.
[Route("api/extras")]
[ApiController]
public class KitController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<KitDto>>> GetActiveKits(CancellationToken ct)
    {
        var kits = await db.Kits
            .AsNoTracking()
            .Where(k => k.IsActive)
            .OrderBy(k => k.Order)
            .Select(k => new KitDto(k.Id, k.Name, k.Description, k.Price, k.Items))
            .ToListAsync(ct);

        return Ok(kits);
    }
}
