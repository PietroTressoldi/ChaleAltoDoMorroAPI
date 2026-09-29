using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

// Uma rota para as duas telas do portal: "Pré-reservas" (status=pending) e "Reservas" (status=confirmed).
[Route("api/admin/reservations")]
public class AdminReservationController(SrvAdminReservation srvAdminReservation) : AdminControllerBase
{
    // Filtro inválido (sort=banana, pageSize=500...) nem chega aqui: o [ApiController] devolve 400 com ProblemDetails.
    [HttpGet]
    public async Task<ActionResult<Paged<ReservationListItemDto>>> List([FromQuery] ReservationQuery query, CancellationToken ct) =>
        Ok(await srvAdminReservation.List(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReservationDetailDto>> Get(Guid id, CancellationToken ct) =>
        ToActionResult(await srvAdminReservation.Get(id, ct));

    // O contrato aceita um corpo com a folga ({ bufferBeforeNights, bufferAfterNights }), mas ela só existe na task 16.
    // Sem parâmetro [FromBody], o ASP.NET simplesmente ignora o corpo se o portal mandar.
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationDetailDto>> Confirm(Guid id, CancellationToken ct) =>
        ToActionResult(await srvAdminReservation.Confirm(id, ct));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationDetailDto>> Cancel(Guid id, CancellationToken ct) =>
        ToActionResult(await srvAdminReservation.Cancel(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ReservationDetailDto>> UpdateNotes(Guid id, UpdateReservationNotesRequest request, CancellationToken ct) =>
        ToActionResult(await srvAdminReservation.UpdateNotes(id, request, ct));
}
