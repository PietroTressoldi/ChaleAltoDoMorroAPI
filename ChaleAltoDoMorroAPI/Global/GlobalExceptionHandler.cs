using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Global;

// Última rede de proteção: qualquer exceção que nenhum código tratou vira 500 em ProblemDetails, com log.
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment env,
    ILogger<GlobalExceptionHandler> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        // Cliente desistiu (fechou a aba, caiu a internet do celular): não é erro nosso.
        if (exception is OperationCanceledException && http.RequestAborted.IsCancellationRequested)
        {
            log.LogInformation("Requisição {Method} {Path} cancelada pelo cliente.", http.Request.Method, http.Request.Path);
            http.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        // Mesmo traceId que vai na resposta: o dono manda pelo WhatsApp e você acha a linha no log.
        // Path (sem query string): a busca de hóspedes pode ter nome ou CPF na query.
        var traceId = Activity.Current?.Id ?? http.TraceIdentifier;
        log.LogError(exception, "Erro inesperado em {Method} {Path} (traceId {TraceId}).",
            http.Request.Method, http.Request.Path, traceId);

        http.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Erro inesperado",
                // Mensagem da exceção só em dev: em produção ela pode revelar SQL, nomes de tabela, caminhos do servidor.
                Detail = env.IsDevelopment()
                    ? exception.Message
                    : "Tente de novo em instantes. Se continuar, envie o traceId para o suporte."
            }
        });
    }
}
