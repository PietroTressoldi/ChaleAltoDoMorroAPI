using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

public class SrvPackage(AppDbContext db, SrvAvailability srvAvailability, IClock brazilClock)
{
    // Padrão: só pacotes que ainda não terminaram (o portal raramente quer o histórico).
    public async Task<List<AdminPackageDto>> GetAll(bool includePast, CancellationToken ct = default)
    {
        var today = brazilClock.Today();

        var packages = await db.SpecialPackages
            .AsNoTracking()
            .Where(p => includePast || p.CheckOut > today)
            .OrderBy(p => p.CheckIn)
            .ToListAsync(ct);

        var result = new List<AdminPackageDto>();
        foreach (var package in packages)
            result.Add(ToDto(package, await CountPendingIn(package.CheckIn, package.CheckOut, ct)));

        return result;
    }

    public async Task<AdminResult<AdminPackageDto>> Get(Guid id, CancellationToken ct = default)
    {
        var package = await db.SpecialPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

        return package is null
            ? NotFoundResult()
            : new Success<AdminPackageDto>(ToDto(package, await CountPendingIn(package.CheckIn, package.CheckOut, ct)));
    }

    public async Task<AdminResult<AdminPackageDto>> Create(SavePackageRequest request, CancellationToken ct = default)
    {
        var problem = await Validate(request, ignoreId: null, ct);
        if (problem is not null)
            return problem;

        var package = new SpecialPackage();
        Apply(package, request);
        db.SpecialPackages.Add(package);
        await db.SaveChangesAsync(ct);

        return new Success<AdminPackageDto>(ToDto(package, await CountPendingIn(package.CheckIn, package.CheckOut, ct)));
    }

    public async Task<AdminResult<AdminPackageDto>> Update(Guid id, SavePackageRequest request, CancellationToken ct = default)
    {
        var package = await db.SpecialPackages.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (package is null)
            return NotFoundResult();

        // ignoreId: o próprio pacote não "se sobrepõe a si mesmo".
        var problem = await Validate(request, ignoreId: id, ct);
        if (problem is not null)
            return problem;

        Apply(package, request);
        await db.SaveChangesAsync(ct);

        return new Success<AdminPackageDto>(ToDto(package, await CountPendingIn(package.CheckIn, package.CheckOut, ct)));
    }

    // Apagar é seguro: a reserva guarda uma cópia (PackageId/PackageName) do pacote do momento em que foi feita.
    public async Task<bool> Delete(Guid id, CancellationToken ct = default) =>
        await db.SpecialPackages.Where(p => p.Id == id).ExecuteDeleteAsync(ct) > 0;

    private async Task<AdminResult<AdminPackageDto>?> Validate(SavePackageRequest request, Guid? ignoreId, CancellationToken ct)
    {
        if (request.CheckOut <= request.CheckIn)
            return new Invalid<AdminPackageDto>("O check-out precisa ser depois do check-in.");

        if (request.CheckIn < brazilClock.Today())
            return new Invalid<AdminPackageDto>("O pacote não pode começar no passado.");

        // Candidatos pelo banco (filtro largo) e a decisão pela regra pura testada (PackageRules.Overlaps).
        var candidates = await db.SpecialPackages
            .AsNoTracking()
            .Where(p => p.Id != ignoreId && p.CheckOut > request.CheckIn)
            .ToListAsync(ct);

        var overlapping = candidates.FirstOrDefault(p =>
            PackageRules.Overlaps(request.CheckIn, request.CheckOut, p.CheckIn, p.CheckOut));

        if (overlapping is not null)
            return new Conflict<AdminPackageDto>(
                $"As datas se sobrepõem ao pacote \"{overlapping.Name}\" ({overlapping.CheckIn:dd/MM/yyyy} a {overlapping.CheckOut:dd/MM/yyyy}).");

        // Pacote é vendido fechado: não dá para vender noites que já estão bloqueadas ou reservadas.
        var unavailable = (await srvAvailability.GetUnavailableNights(ct)).ToHashSet();
        var busyNight = BookingRules.Nights(request.CheckIn, request.CheckOut).FirstOrDefault(unavailable.Contains);

        if (busyNight != default)
            return new Conflict<AdminPackageDto>($"A noite de {busyNight:dd/MM/yyyy} já está bloqueada ou reservada.");

        return null;
    }

    private async Task<int> CountPendingIn(DateOnly checkIn, DateOnly checkOut, CancellationToken ct) =>
        await db.Reservations.CountAsync(r =>
            r.Status == ReservationStatus.Pending && r.CheckIn < checkOut && checkIn < r.CheckOut, ct);

    private static void Apply(SpecialPackage package, SavePackageRequest request)
    {
        package.Name = request.Name;
        package.Description = request.Description;
        package.CheckIn = request.CheckIn;
        package.CheckOut = request.CheckOut;
        package.Price = request.Price;
    }

    private static AdminPackageDto ToDto(SpecialPackage p, int pending) =>
        new(p.Id, p.Name, p.Description, p.CheckIn, p.CheckOut, p.Price, pending);

    private static NotFound<AdminPackageDto> NotFoundResult() => new("Pacote não encontrado.");
}
