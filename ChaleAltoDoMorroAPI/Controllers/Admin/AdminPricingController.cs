using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

// "Admin" no nome: já existe um PricingController público, e o Scalar agrupa pelo nome da classe.
[Route("api/admin/pricing")]
public class AdminPricingController(SrvPricing srvPricing) : AdminControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PricingRuleDto>>> GetAll(CancellationToken ct) =>
        Ok(await srvPricing.GetAll(ct));

    // {kind} chega como texto ("weekday"/"weekend"), igual ao contrato.
    [HttpGet("{kind}")]
    public async Task<ActionResult<PricingRuleDto>> Get(PricingKind kind, CancellationToken ct) =>
        ToActionResult(await srvPricing.Get(kind, ct));

    [HttpPut("{kind}")]
    public async Task<ActionResult<PricingRuleDto>> Put(PricingKind kind, PutPricingRuleRequest request, CancellationToken ct) =>
        ToActionResult(await srvPricing.Put(kind, request, ct));
}
