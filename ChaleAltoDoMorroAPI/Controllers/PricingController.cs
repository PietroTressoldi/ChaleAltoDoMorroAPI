using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Controllers;

[Route("api/pricing")]
[ApiController]
public class PricingController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PricingRuleDto>>> GetPricingRules(CancellationToken ct)
    {
        var pricingRules = await db.PricingRules
            .AsNoTracking()
            .Select(p => new PricingRuleDto(p.Kind, p.Label, p.PricePerNight, p.MinNights))
            .ToListAsync(ct);

        return Ok(pricingRules);
    }
}
