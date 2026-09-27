using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

[Route("api/admin/blocked-dates")]
public class AdminBlockedDateController(SrvBlockedDate srvBlockedDate) : AdminControllerBase
{
    // ?from=2026-10-01&to=2026-12-31 (sem from = a partir de hoje).
    [HttpGet]
    public async Task<ActionResult<List<AdminBlockedDateDto>>> GetAll([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        Ok(await srvBlockedDate.GetAll(from, to, ct));

    [HttpPost]
    public async Task<ActionResult<BlockedRangeCreatedDto>> CreateRange(CreateBlockedRangeRequest request, CancellationToken ct) =>
        ToActionResult(await srvBlockedDate.CreateRange(request, ct));

    // A data é a chave natural (índice único): DELETE /api/admin/blocked-dates/2026-10-14.
    [HttpDelete("{date}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(DateOnly date, CancellationToken ct) =>
        await srvBlockedDate.Delete(date, ct)
            ? NoContent()
            : Problem(title: "Não encontrado", detail: "Não há bloqueio manual nessa data.", statusCode: StatusCodes.Status404NotFound);
}
