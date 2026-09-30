using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

[Route("api/admin/cash-categories")]
public class AdminCashCategoryController(SrvCashCategory srvCashCategory) : AdminControllerBase
{
    // ?type=expense&includeInactive=false. Sem type, vêm as de entrada e as de saída.
    [HttpGet]
    public async Task<ActionResult<List<CashCategoryDto>>> GetAll(
        [FromQuery] CashTransactionType? type, [FromQuery] bool includeInactive, CancellationToken ct) =>
        Ok(await srvCashCategory.GetAll(type, includeInactive, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CashCategoryDto>> Get(int id, CancellationToken ct) =>
        ToActionResult(await srvCashCategory.Get(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CashCategoryDto>> Create(SaveCashCategoryRequest request, CancellationToken ct)
    {
        var result = await srvCashCategory.Create(request, ct);
        return result is Success<CashCategoryDto> created
            ? CreatedAtAction(nameof(Get), new { id = created.Value.Id }, created.Value)
            : ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CashCategoryDto>> Update(int id, SaveCashCategoryRequest request, CancellationToken ct) =>
        ToActionResult(await srvCashCategory.Update(id, request, ct));

    // 204 = apagada. 200 com aviso = já tinha uso, então só foi desativada (o portal mostra a mensagem).
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) => await srvCashCategory.Delete(id, ct) switch
    {
        CashCategoryDeleteOutcome.Deleted => NoContent(),
        CashCategoryDeleteOutcome.Deactivated => Ok(new
        {
            deactivated = true,
            message = "A categoria já tem lançamentos ou despesas fixas: foi desativada em vez de apagada."
        }),
        _ => Problem(title: "Não encontrado", detail: "Categoria não encontrada.", statusCode: StatusCodes.Status404NotFound)
    };
}