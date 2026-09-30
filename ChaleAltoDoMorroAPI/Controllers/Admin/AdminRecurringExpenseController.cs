
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

[Route("api/admin/recurring-expenses")]
public class AdminRecurringExpenseController(SrvRecurringExpense srvRecurringExpense) : AdminControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RecurringExpenseDto>>> GetAll(CancellationToken ct) =>
        Ok(await srvRecurringExpense.GetAll(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecurringExpenseDto>> Get(int id, CancellationToken ct) =>
        ToActionResult(await srvRecurringExpense.Get(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RecurringExpenseDto>> Create(SaveRecurringExpenseRequest request, CancellationToken ct)
    {
        var result = await srvRecurringExpense.Create(request, ct);
        return result is Success<RecurringExpenseDto> created
            ? CreatedAtAction(nameof(Get), new { id = created.Value.Id }, created.Value)
            : ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecurringExpenseDto>> Update(int id, SaveRecurringExpenseRequest request, CancellationToken ct) =>
        ToActionResult(await srvRecurringExpense.Update(id, request, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await srvRecurringExpense.Delete(id, ct)
            ? NoContent()
            : Problem(title: "Não encontrado", detail: "Despesa fixa não encontrada.", statusCode: StatusCodes.Status404NotFound);
}