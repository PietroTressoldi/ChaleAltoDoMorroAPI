using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

// Base de todos os controllers do portal: [Authorize] herdado (nenhum esquece) e a tradução AdminResult → HTTP.
[ApiController]
[Authorize]
public abstract class AdminControllerBase : ControllerBase
{
    protected ActionResult<T> ToActionResult<T>(AdminResult<T> result) => result switch
    {
        Success<T> success => Ok(success.Value),
        NotFound<T> notFound => Problem(title: "Não encontrado", detail: notFound.Message, statusCode: StatusCodes.Status404NotFound),
        Conflict<T> conflict => Problem(title: "Conflito", detail: conflict.Message, statusCode: StatusCodes.Status409Conflict),
        Invalid<T> invalid => Problem(title: "Dados inválidos", detail: invalid.Message, statusCode: StatusCodes.Status422UnprocessableEntity),
        _ => throw new InvalidOperationException($"Resultado não tratado: {result.GetType().Name}")
    };
}
