using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Public;

[Route("api/gallery")]
[ApiController]
public class GalleryController(SrvGallery srvGallery) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GalleryImageDto>>> GetGallery(CancellationToken ct) =>
        Ok(await srvGallery.GetAll(ct));
}
