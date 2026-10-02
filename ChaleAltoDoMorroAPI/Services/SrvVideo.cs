using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Interfaces;
using ChaleAltoDoMorroAPI.Storage;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;


public class SrvVideo(AppDbContext db, IFileStorage storage)
{
    public const long MaxVideoBytes = 100 * 1024 * 1024;
    public const long MaxPosterBytes = SrvGallery.MaxImageBytes;
    private const string Folder = "videos";

    public async Task<List<VideosDto>> GetAll(CancellationToken ct = default)
    {
        var videos = await db.TourVideos
            .AsNoTracking()
            .OrderBy(v => v.Order)
            .ToListAsync(ct);

        return videos.Select(ToDto).ToList();
    }

    public async Task<AdminResult<VideosDto>> Get(Guid id, CancellationToken ct = default)
    {
        var video = await db.TourVideos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        return video is null ? NotFoundResult() : new Success<VideosDto>(ToDto(video));
    }

    public async Task<AdminResult<VideosDto>> CreateYoutube(CreateYoutubeVideoRequest request, CancellationToken ct = default)
    {
        if (!YoutubeUrl.IsValid(request.Url))
            return InvalidUrl();

        var video = new TourVideo
        {
            Title = request.Title,
            Description = request.Description,
            Url = request.Url,
            Orientation = request.Orientation,
            Order = await NextOrder(ct)
        };

        db.TourVideos.Add(video);
        await db.SaveChangesAsync(ct);
        return new Success<VideosDto>(ToDto(video));
    }

    public async Task<AdminResult<VideosDto>> Upload(UploadVideoForm form, CancellationToken ct = default)
    {
        if (form.File.Length == 0 || form.File.Length > MaxVideoBytes)
            return new Invalid<VideosDto>("O vídeo precisa ter até 100 MB.");

        if (await form.File.DetectTypeAsync(ct) != FileSignature.Mp4)
            return new Invalid<VideosDto>("Envie o vídeo em MP4.");

        FileType? posterType = null;
        if (form.Poster is not null)
        {
            if (form.Poster.Length == 0 || form.Poster.Length > MaxPosterBytes)
                return new Invalid<VideosDto>("A capa precisa ter até 10 MB.");

            posterType = await form.Poster.DetectTypeAsync(ct);
            if (posterType is null || !FileSignature.Images.Contains(posterType))
                return new Invalid<VideosDto>("A capa precisa ser JPEG, PNG ou WebP.");
        }

        var order = await NextOrder(ct);
        var savedKeys = new List<string>();

        try
        {
            var video = new TourVideo
            {
                Title = form.Title,
                Description = form.Description,
                Orientation = form.Orientation,
                Order = order,
                FileKey = await Save(form.File, FileSignature.Mp4, savedKeys, ct),
                PosterKey = posterType is null ? null : await Save(form.Poster!, posterType, savedKeys, ct)
            };

            db.TourVideos.Add(video);
            await db.SaveChangesAsync(ct);
            return new Success<VideosDto>(ToDto(video));
        }
        catch
        {
            // Falhou no meio (capa, banco, cancelamento): apaga o que já tinha ido para o disco.
            foreach (var key in savedKeys)
                await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }
    }

    public async Task<AdminResult<VideosDto>> Update(Guid id, UpdateVideoRequest request, CancellationToken ct = default)
    {
        var video = await db.TourVideos.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video is null)
            return NotFoundResult();

        if (request.Url is not null)
        {
            if (video.FileKey is not null)
                return new Invalid<VideosDto>("Este vídeo foi enviado como arquivo, então não tem link. Para trocar, remova e envie de novo.");

            if (!YoutubeUrl.IsValid(request.Url))
                return InvalidUrl();

            video.Url = request.Url;
        }

        video.Title = request.Title;
        video.Description = request.Description;
        video.Orientation = request.Orientation;
        await db.SaveChangesAsync(ct);
        return new Success<VideosDto>(ToDto(video));
    }

    // null = deu certo; texto = motivo do 422.
    public async Task<string?> Reorder(List<Guid> ids, CancellationToken ct = default)
    {
        var videos = await db.TourVideos.ToListAsync(ct);
        if (!MediaOrder.IsCompleteList(videos.Select(v => v.Id).ToList(), ids))
            return "Mande todos os vídeos, cada um uma vez, na nova ordem.";

        var byId = videos.ToDictionary(v => v.Id);
        for (var position = 0; position < ids.Count; position++)
            byId[ids[position]].Order = position;

        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<bool> Delete(Guid id, CancellationToken ct = default)
    {
        var video = await db.TourVideos.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video is null)
            return false;

        db.TourVideos.Remove(video);
        await db.SaveChangesAsync(ct);

        if (video.FileKey is not null)
            await storage.DeleteAsync(video.FileKey, CancellationToken.None);
        if (video.PosterKey is not null)
            await storage.DeleteAsync(video.PosterKey, CancellationToken.None);

        return true;
    }

    private async Task<string> Save(IFormFile file, FileType type, List<string> savedKeys, CancellationToken ct)
    {
        await using var content = file.OpenReadStream();
        var key = await storage.SaveAsync(content, type, Folder, ct);
        savedKeys.Add(key);
        return key;
    }

    private async Task<int> NextOrder(CancellationToken ct) =>
        (await db.TourVideos.MaxAsync(v => (int?)v.Order, ct) ?? -1) + 1;

    // O site recebe um campo "url" só: o mp4 nosso (URL montada) ou o link do YouTube.
    private VideosDto ToDto(TourVideo v) => new(
        v.Id, v.Title, v.Description,
        v.FileKey is not null ? storage.GetPublicUrl(v.FileKey) : v.Url!,
        v.PosterKey is null ? null : storage.GetPublicUrl(v.PosterKey),
        v.Orientation, v.Order);

    private static Invalid<VideosDto> InvalidUrl() => new("Use um link do YouTube (watch?v=, youtu.be/, shorts/ ou embed/).");

    private static NotFound<VideosDto> NotFoundResult() => new("Vídeo não encontrado.");
}