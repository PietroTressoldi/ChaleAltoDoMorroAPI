using System.ComponentModel.DataAnnotations;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

[Route("api/admin/cashbooks")]
public class AdminCashBookController(SrvCashBook srvCashBook) : AdminControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CashBookSummaryDto>>> List([FromQuery] CashBookQuery query, CancellationToken ct) =>
        Ok(await srvCashBook.List(query, ct));

    // int? + [Required]: parâmetro ausente não é validado, então sem o nullable o ano viraria 0 com 200.
    [HttpGet("year-summary")]
    public async Task<ActionResult<CashYearSummaryDto>> YearSummary(
        [FromQuery, Required, Range(2020, 2100)] int? year, CancellationToken ct) =>
        Ok(await srvCashBook.YearSummary(year!.Value, ct));

    // range(1,12) na rota: /cashbooks/2026/13 nem chega aqui (404 de rota).
    [HttpGet("{year:int}/{month:int:range(1,12)}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CashBookDetailDto>> GetByPeriod(int year, int month, CancellationToken ct) =>
        ToActionResult(await srvCashBook.GetByPeriod(year, month, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashBookDetailDto>> Create(CreateCashBookRequest request, CancellationToken ct)
    {
        var result = await srvCashBook.Create(request, ct);
        return result is Success<CashBookDetailDto> created
            ? CreatedAtAction(nameof(GetByPeriod), new { year = created.Value.Year, month = created.Value.Month }, created.Value)
            : ToActionResult(result);
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<CashBookSummaryDto>> UpdateNotes(int id, UpdateCashBookNotesRequest request, CancellationToken ct) =>
        ToActionResult(await srvCashBook.UpdateNotes(id, request, ct));

    [HttpPost("{id:int}/close")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashBookSummaryDto>> Close(int id, CancellationToken ct) =>
        ToActionResult(await srvCashBook.Close(id, ct));

    [HttpPost("{id:int}/reopen")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashBookSummaryDto>> Reopen(int id, CancellationToken ct) =>
        ToActionResult(await srvCashBook.Reopen(id, ct));

    [HttpPost("{id:int}/apply-recurring")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplyRecurringResponse>> ApplyRecurring(int id, CancellationToken ct) =>
        ToActionResult(await srvCashBook.ApplyRecurring(id, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await srvCashBook.Delete(id, ct);
        if (result is Success<bool>)
            return NoContent();

        return ToActionResult(result).Result!;
    }
}