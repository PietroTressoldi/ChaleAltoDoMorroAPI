using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

[Route("api/admin/videos")]
public class AdminVideoController(SrvVideo srvVideo) : AdminControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VideosDto>>> GetAll(CancellationToken ct) =>
        Ok(await srvVideo.GetAll(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<VideosDto>> Get(Guid id, CancellationToken ct) =>
        ToActionResult(await srvVideo.Get(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VideosDto>> CreateYoutube(CreateYoutubeVideoRequest request, CancellationToken ct)
    {
        var result = await srvVideo.CreateYoutube(request, ct);
        if (result is Success<VideosDto> success)
            return CreatedAtAction(nameof(Get), new { id = success.Value.Id }, success.Value);

        return ToActionResult(result);
    }

    // Vídeo de até 100 MB + capa de até 10 MB + os campos do formulário.
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(SrvVideo.MaxVideoBytes + SrvVideo.MaxPosterBytes + 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<VideosDto>> Upload([FromForm] UploadVideoForm form, CancellationToken ct)
    {
        var result = await srvVideo.Upload(form, ct);
        if (result is Success<VideosDto> success)
            return CreatedAtAction(nameof(Get), new { id = success.Value.Id }, success.Value);

        return ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<VideosDto>> Update(Guid id, UpdateVideoRequest request, CancellationToken ct) =>
        ToActionResult(await srvVideo.Update(id, request, ct));

    [HttpPut("reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken ct)
    {
        var error = await srvVideo.Reorder(request.Ids, ct);
        return error is null
            ? NoContent()
            : Problem(title: "Dados inválidos", detail: error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await srvVideo.Delete(id, ct)
            ? NoContent()
            : Problem(title: "Não encontrado", detail: "Vídeo não encontrado.", statusCode: StatusCodes.Status404NotFound);
}
