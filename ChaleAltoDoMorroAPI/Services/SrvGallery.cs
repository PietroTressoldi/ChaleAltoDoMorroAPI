using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Interfaces;
using ChaleAltoDoMorroAPI.Storage;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services
{
    public class SrvGallery(AppDbContext db, IFileStorage storage)
    {
        public const long MaxImageBytes = 10 * 1024 * 1024;
        private const string Folder = "galeria";

        // Site e portal leem igual. A URL é montada DEPOIS do ToListAsync: o banco devolve a chave, o C# monta a URL.
        public async Task<List<GalleryImageDto>> GetAll(CancellationToken ct = default)
        {
            var images = await db.GalleryImages
                .AsNoTracking()
                .OrderBy(i => i.Order)
                .ToListAsync(ct);

            return images.Select(ToDto).ToList();
        }

        public async Task<AdminResult<GalleryImageDto>> Get(Guid id, CancellationToken ct = default)
        {
            var image = await db.GalleryImages.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
            return image is null ? NotFoundResult() : new Success<GalleryImageDto>(ToDto(image));
        }

        public async Task<AdminResult<GalleryImageDto>> Upload(IFormFile file, string alt, CancellationToken ct = default)
        {
            if (file.Length == 0 || file.Length > MaxImageBytes)
                return new Invalid<GalleryImageDto>("A foto precisa ter até 10 MB.");

            var type = await file.DetectTypeAsync(ct);
            if (type is null || !FileSignature.Images.Contains(type))
                return new Invalid<GalleryImageDto>("Envie uma foto em JPEG, PNG ou WebP.");

            // A foto nova entra no fim da galeria.
            var lastOrder = await db.GalleryImages.MaxAsync(i => (int?)i.Order, ct) ?? -1;

            await using var content = file.OpenReadStream();
            var key = await storage.SaveAsync(content, type, Folder, ct);

            var image = new GalleryImage { FileKey = key, Alt = alt, Order = lastOrder + 1 };
            db.GalleryImages.Add(image);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch
            {
                // O banco falhou: apaga o arquivo para não sobrar órfão no disco.
                await storage.DeleteAsync(key, CancellationToken.None);
                throw;
            }

            return new Success<GalleryImageDto>(ToDto(image));
        }

        public async Task<AdminResult<GalleryImageDto>> UpdateAlt(Guid id, string alt, CancellationToken ct = default)
        {
            var image = await db.GalleryImages.FirstOrDefaultAsync(i => i.Id == id, ct);
            if (image is null)
                return NotFoundResult();

            image.Alt = alt;
            await db.SaveChangesAsync(ct);
            return new Success<GalleryImageDto>(ToDto(image));
        }

        // null = deu certo; texto = motivo do 422.
        public async Task<string?> Reorder(List<Guid> ids, CancellationToken ct = default)
        {
            var images = await db.GalleryImages.ToListAsync(ct);
            if (!MediaOrder.IsCompleteList(images.Select(i => i.Id).ToList(), ids))
                return "Mande todas as fotos da galeria, cada uma uma vez, na nova ordem.";

            var byId = images.ToDictionary(i => i.Id);
            for (var position = 0; position < ids.Count; position++)
                byId[ids[position]].Order = position;

            await db.SaveChangesAsync(ct);
            return null;
        }

        public async Task<bool> Delete(Guid id, CancellationToken ct = default)
        {
            var image = await db.GalleryImages.FirstOrDefaultAsync(i => i.Id == id, ct);
            if (image is null)
                return false;

            // Banco primeiro: se o arquivo não apagar, sobra um órfão no disco (inofensivo).
            // Na ordem contrária, o site poderia mostrar uma foto quebrada.
            db.GalleryImages.Remove(image);
            await db.SaveChangesAsync(ct);
            await storage.DeleteAsync(image.FileKey, CancellationToken.None);
            return true;
        }

        // SrcSet null: as variantes 640/1280/1920 são a Parte F (opcional). O JSON omite o campo.
        private GalleryImageDto ToDto(GalleryImage i) =>
            new(i.Id, storage.GetPublicUrl(i.FileKey), null, i.Alt, i.Order);

        private static NotFound<GalleryImageDto> NotFoundResult() => new("Foto não encontrada.");
    }
}
