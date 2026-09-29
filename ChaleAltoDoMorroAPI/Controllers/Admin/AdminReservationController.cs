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
}
