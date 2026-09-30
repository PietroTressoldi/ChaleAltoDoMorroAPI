using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

// Duas bases de URL no mesmo controller: criar é "dentro do livro", editar/apagar é pelo id da transação.
[Route("api/admin")]
public class AdminCashTransactionController(SrvCashTransaction srvCashTransaction) : AdminControllerBase
{
    [HttpPost("cashbooks/{cashBookId:int}/transactions")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CashTransactionDto>> Create(int cashBookId, SaveCashTransactionRequest request, CancellationToken ct)
    {
        var result = await srvCashTransaction.Create(cashBookId, request, ct);

        // Não existe GET de uma transação só (o portal lê o mês inteiro), então o 201 vai sem Location.
        return result is Success<CashTransactionDto> created
            ? StatusCode(StatusCodes.Status201Created, created.Value)
            : ToActionResult(result);
    }

    [HttpPut("cash-transactions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CashTransactionDto>> Update(Guid id, SaveCashTransactionRequest request, CancellationToken ct) =>
        ToActionResult(await srvCashTransaction.Update(id, request, ct));

    [HttpDelete("cash-transactions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await srvCashTransaction.Delete(id, ct);
        if (result is Success<bool>)
            return NoContent();

        return ToActionResult(result).Result!;
    }
}