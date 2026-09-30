using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

// Tela "Hóspedes" do portal. CPF só no detalhe, nunca na lista.
[Route("api/admin/guests")]
public class AdminGuestController(SrvGuest srvGuest) : AdminControllerBase
{
    [HttpGet]
    public async Task<ActionResult<Paged<GuestListItemDto>>> List([FromQuery] GuestQuery query, CancellationToken ct) =>
        Ok(await srvGuest.List(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GuestDetailDto>> Get(Guid id, CancellationToken ct) =>
        ToActionResult(await srvGuest.Get(id, ct));

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuestDetailDto>> Update(Guid id, UpdateGuestRequest request, CancellationToken ct) =>
        ToActionResult(await srvGuest.Update(id, request, ct));
}
