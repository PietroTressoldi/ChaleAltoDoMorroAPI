using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Admin;

[Route("api/admin/gallery")]
public class AdminGalleryController(SrvGallery srvGallery) : AdminControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GalleryImageDto>>> GetAll(CancellationToken ct) =>
        Ok(await srvGallery.GetAll(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GalleryImageDto>> Get(Guid id, CancellationToken ct) =>
        ToActionResult(await srvGallery.Get(id, ct));

    // O Kestrel corta o corpo em ~30 MB por padrão. Aqui o limite fica um pouco acima da foto (o resto são os campos do formulário).
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(SrvGallery.MaxImageBytes + 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GalleryImageDto>> Upload([FromForm] UploadGalleryImageForm form, CancellationToken ct)
    {
        var result = await srvGallery.Upload(form.File, form.Alt, ct);
        if (result is Success<GalleryImageDto> success)
            return CreatedAtAction(nameof(Get), new { id = success.Value.Id }, success.Value);

        return ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GalleryImageDto>> UpdateAlt(Guid id, UpdateGalleryImageRequest request, CancellationToken ct) =>
        ToActionResult(await srvGallery.UpdateAlt(id, request.Alt, ct));

    [HttpPut("reorder")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken ct)
    {
        var error = await srvGallery.Reorder(request.Ids, ct);
        return error is null
            ? NoContent()
            : Problem(title: "Dados inválidos", detail: error, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await srvGallery.Delete(id, ct)
            ? NoContent()
            : Problem(title: "Não encontrado", detail: "Foto não encontrada.", statusCode: StatusCodes.Status404NotFound);
}