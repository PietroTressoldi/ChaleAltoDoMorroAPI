using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Global;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ChaleAltoDoMorroAPI.Controllers.Public;

// Controller fino: a regra fica no SrvReservation; aqui só se traduz o resultado em HTTP.
// Campos inválidos nem chegam aqui: o [ApiController] devolve 400 (ValidationProblemDetails) sozinho.
[Route("api/pre-reservations")]
[ApiController]
public class PreReservationController(SrvReservation srvReservation) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.PreReservation)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreatePreReservation(PostPreReservationRequest request, CancellationToken ct)
    {
        var response = await srvReservation.CreatePreReservation(request, ct);

        return ConvertResponseToResult(response);
    }


    // 409 = conflito com a disponibilidade atual; 422 = regra de negócio; 5xx = problema de cadastro (não do visitante).
    // As mensagens não têm dados pessoais: a rota é pública.
    private IActionResult ConvertResponseToResult(PreReservationResponse response) => response switch
    {
        Domain.Created created => Created(
            $"/api/admin/pre-reservations/{created.id}",
            new { id = created.id, total = created.Total, status = "pending" }),

        StayRejected { error: BlockedNight blocked } => Problem(
            title: "Data indisponível",
            detail: $"A noite de {blocked.Night:dd/MM/yyyy} não está disponível.",
            statusCode: StatusCodes.Status409Conflict),

        StayRejected { error: PartialPackage partial } => Problem(
            title: "Datas de pacote especial",
            detail: $"Estas datas fazem parte do pacote {partial.Package.Name} " +
                    $"({partial.Package.CheckIn:dd/MM} a {partial.Package.CheckOut:dd/MM}), vendido fechado.",
            statusCode: StatusCodes.Status409Conflict),

        StayRejected { error: MinNightsNotMet minNights } => Problem(
            title: "Mínimo de diárias",
            detail: $"Estadias com sexta ou sábado exigem no mínimo {minNights.Required} diárias.",
            statusCode: StatusCodes.Status422UnprocessableEntity),

        StayRejected { error: PastCheckIn } => Problem(
            title: "Check-in no passado",
            detail: "A data de check-in já passou.",
            statusCode: StatusCodes.Status422UnprocessableEntity),

        StayRejected { error: CheckOutNotAfterCheckIn } => Problem(
            title: "Datas inválidas",
            detail: "O check-out precisa ser depois do check-in.",
            statusCode: StatusCodes.Status422UnprocessableEntity),

        StayRejected { error: InvalidDate } => Problem(
            title: "Datas inválidas",
            detail: "Informe as datas de check-in e check-out.",
            statusCode: StatusCodes.Status400BadRequest),

        PackageMismatch => Problem(
            title: "Pacote não confere",
            detail: "O pacote informado não corresponde às datas escolhidas.",
            statusCode: StatusCodes.Status422UnprocessableEntity),

        KitNotFound => Problem(
            title: "Kit indisponível",
            detail: "O kit escolhido não existe ou não está mais disponível.",
            statusCode: StatusCodes.Status422UnprocessableEntity),

        PricingNotConfigured => Problem(
            title: "Preços não configurados",
            detail: "Não foi possível calcular o valor da estadia. Tente novamente mais tarde.",
            statusCode: StatusCodes.Status503ServiceUnavailable),

        // Se alguém criar um resultado novo e esquecer de tratar aqui, a falha aparece na hora.
        _ => throw new InvalidOperationException($"Resultado não tratado: {response.GetType().Name}")
    };
}
