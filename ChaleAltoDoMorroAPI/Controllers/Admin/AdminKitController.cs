using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

[Route("api/admin/kits")]
public class AdminKitController(SrvKit srvKit) : AdminControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminKitDto>>> GetAll(CancellationToken ct) =>
        Ok(await srvKit.GetAll(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminKitDto>> Get(Guid id, CancellationToken ct) =>
        ToActionResult(await srvKit.Get(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminKitDto>> Create(SaveKitRequest request, CancellationToken ct)
    {
        var kit = await srvKit.Create(request, ct);
        return CreatedAtAction(nameof(Get), new { id = kit.Id }, kit);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminKitDto>> Update(Guid id, SaveKitRequest request, CancellationToken ct) =>
        ToActionResult(await srvKit.Update(id, request, ct));

    [HttpPatch("{id:guid}/active")]
    public async Task<ActionResult<AdminKitDto>> SetActive(Guid id, SetKitActiveRequest request, CancellationToken ct) =>
        ToActionResult(await srvKit.SetActive(id, request.IsActive, ct));

    // 204 = apagado. 200 com aviso = estava em reserva, então só foi desativado (o portal mostra a mensagem).
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => await srvKit.Delete(id, ct) switch
    {
        KitDeleteOutcome.Deleted => NoContent(),
        KitDeleteOutcome.Deactivated => Ok(new { deactivated = true, message = "O kit já foi usado em reservas: foi desativado em vez de apagado." }),
        _ => Problem(title: "Não encontrado", detail: "Kit não encontrado.", statusCode: StatusCodes.Status404NotFound)
    };
}
