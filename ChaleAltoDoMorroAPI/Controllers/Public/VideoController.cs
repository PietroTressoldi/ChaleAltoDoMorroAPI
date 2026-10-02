using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChaleAltoDoMorroAPI.Controllers.Public;

[Route("api/videos")]
[ApiController]
public class VideoController(SrvVideo srvVideo) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VideosDto>>> GetVideos(CancellationToken ct) =>
        Ok(await srvVideo.GetAll(ct));
}