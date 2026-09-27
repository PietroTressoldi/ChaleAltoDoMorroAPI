using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

[Route("api/admin/packages")]
public class AdminPackageController(SrvPackage srvPackage) : AdminControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminPackageDto>>> GetAll([FromQuery] bool includePast, CancellationToken ct) =>
        Ok(await srvPackage.GetAll(includePast, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminPackageDto>> Get(Guid id, CancellationToken ct) =>
        ToActionResult(await srvPackage.Get(id, ct));

    // 201 + header Location apontando para o GET do pacote novo.
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminPackageDto>> Create(SavePackageRequest request, CancellationToken ct)
    {
        var result = await srvPackage.Create(request, ct);

        return result is Success<AdminPackageDto> created
            ? CreatedAtAction(nameof(Get), new { id = created.Value.Id }, created.Value)
            : ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminPackageDto>> Update(Guid id, SavePackageRequest request, CancellationToken ct) =>
        ToActionResult(await srvPackage.Update(id, request, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await srvPackage.Delete(id, ct)
            ? NoContent()
            : Problem(title: "Não encontrado", detail: "Pacote não encontrado.", statusCode: StatusCodes.Status404NotFound);
}
