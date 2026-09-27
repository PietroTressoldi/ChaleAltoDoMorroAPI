using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Controllers;

[Route("api/videos")]
[ApiController]
public class VideoController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VideosDto>>> GetVideos(CancellationToken ct)
    {
        var videos = await db.TourVideos
            .AsNoTracking()
            .OrderBy(v => v.Order)
            .Select(v => new VideosDto(v.Id, v.Title, v.Description, v.Url, v.PosterUrl, v.Orientation, v.Order))
            .ToListAsync(ct);

        return Ok(videos);
    }
}
