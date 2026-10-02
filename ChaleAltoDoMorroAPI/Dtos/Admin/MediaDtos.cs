using System.ComponentModel.DataAnnotations;
using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Dtos.Admin;

// ---------- Galeria ----------

// multipart/form-data: classe com propriedades, o formato mais direto para o [FromForm] e para o Scalar montar o formulário.
public class UploadGalleryImageForm
{
    [Required] public IFormFile File { get; set; } = null!;
    [Required, StringLength(250)] public string Alt { get; set; } = string.Empty;
}

public record UpdateGalleryImageRequest([Required, StringLength(250)] string Alt);

// A lista inteira, na nova ordem (galeria e vídeos usam o mesmo formato).
public record ReorderRequest([Required] List<Guid> Ids);

// ---------- Vídeos ----------

public record CreateYoutubeVideoRequest(
    [Required, StringLength(150)] string Title,
    [StringLength(1000)] string? Description,
    [Required, StringLength(500)] string Url,
    VideoOrientation Orientation = VideoOrientation.Landscape);

public class UploadVideoForm
{
    [Required] public IFormFile File { get; set; } = null!;
    public IFormFile? Poster { get; set; }
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public VideoOrientation Orientation { get; set; } = VideoOrientation.Landscape;
}

// Url: só para vídeo do YouTube. Num mp4 enviado, mande null.
public record UpdateVideoRequest(
    [Required, StringLength(150)] string Title,
    [StringLength(1000)] string? Description,
    VideoOrientation Orientation,
    [StringLength(500)] string? Url);