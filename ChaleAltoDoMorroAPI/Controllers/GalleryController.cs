using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Controllers;

[Route("api/gallery")]
[ApiController]
public class GalleryController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GalleryImageDto>>> GetGallery(CancellationToken ct)
    {
        var images = await db.GalleryImages
            .AsNoTracking()
            .OrderBy(i => i.Order)
            .Select(i => new GalleryImageDto(i.Id, i.Src, i.SrcSet, i.Alt, i.Order))
            .ToListAsync(ct);

        return Ok(images);
    }
}
