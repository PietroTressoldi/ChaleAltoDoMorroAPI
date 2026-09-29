using System.Linq.Expressions;
using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// Gestão das reservas no portal (task 9). A criação pública continua no SrvReservation.
public class SrvAdminReservation(AppDbContext db, IClock brazilClock)
{
    // Um mapeamento só para a lista e para o detalhe. Como Expression, o EF traduz em SELECT com JOIN
    // (r.Guest1.Name vira JOIN em guests, sem Include). Compilado, serve para mapear a entidade já carregada.
    private static readonly Expression<Func<Reservation, ReservationListItemDto>> ToListItem = r =>
        new ReservationListItemDto(
            r.Id, r.Status, r.CheckIn, r.CheckOut, r.Nights,
            r.Guest1.Name, r.Guest2 != null ? r.Guest2.Name : null, r.ContactPhone,
            r.PackageName, r.KitName, r.Total, r.IncludesColonialBreakfast,
            r.CreatedAt, r.ConfirmedAt, r.CancelledAt);

    private static readonly Func<Reservation, ReservationListItemDto> ToListItemCompiled = ToListItem.Compile();

    public async Task<Paged<ReservationListItemDto>> List(ReservationQuery query, CancellationToken ct = default)
    {
        IQueryable<Reservation> reservations = db.Reservations.AsNoTracking();

        // Filtros opcionais: cada "if" acrescenta um AND no WHERE. Nada vai ao banco até o CountAsync/ToListAsync.
        if (query.Status is { Count: > 0 } statuses)
            reservations = reservations.Where(r => statuses.Contains(r.Status));

        if (query.When is { } when)
            reservations = reservations.Where(ReservationFilters.When(when, brazilClock.Today()));

        // A estadia precisa TOCAR o período (contrato): começa até o fim dele e termina a partir do início.
        if (query.From is { } from)
            reservations = reservations.Where(r => r.CheckOut >= from);
        if (query.To is { } to)
            reservations = reservations.Where(r => r.CheckIn <= to);

        if (!string.IsNullOrWhiteSpace(query.Search))
            reservations = ApplySearch(reservations, query.Search.Trim());

        // Conta ANTES de paginar: totalCount é o total do filtro, não o tamanho da página.
        var totalCount = await reservations.CountAsync(ct);

        var items = await ApplySort(reservations, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToListItem)
            .ToListAsync(ct);

        return new Paged<ReservationListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<AdminResult<ReservationDetailDto>> Get(Guid id, CancellationToken ct = default)
    {
        var reservation = await db.Reservations
            .AsNoTracking()
            .Include(r => r.Guest1)
            .Include(r => r.Guest2)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (reservation is null)
            return new NotFound<ReservationDetailDto>("Reserva não encontrada.");

        return new Success<ReservationDetailDto>(new ReservationDetailDto(ToListItemCompiled(reservation))
        {
            Guest1 = ToGuestDto(reservation.Guest1),
            Guest2 = reservation.Guest2 is null ? null : ToGuestDto(reservation.Guest2),
            ContactEmail = reservation.ContactEmail,
            Observations = reservation.Observations,
            PackageId = reservation.PackageId,
            KitId = reservation.KitId,
            KitPrice = reservation.KitPrice,
            NightsTotal = reservation.NightsTotal,
            EstimatedTotalFromSite = reservation.EstimatedTotalFromSite,
            SiteTotalDiverged = reservation.EstimatedTotalFromSite != reservation.Total,
            NightsBreakdown = await GetNightsBreakdown(reservation, ct)
        });
    }

    // Nome do hóspede 1 ou 2, ou telefone. ILike = LIKE sem diferenciar maiúsculas, feito pelo Postgres
    // (ToLower() em C# impediria o uso de índice e erraria em alguns caracteres).
    private static IQueryable<Reservation> ApplySearch(IQueryable<Reservation> reservations, string search)
    {
        var pattern = $"%{EscapeLike(search)}%";

        // O telefone é gravado só com dígitos: "(51) 98909" também precisa achar "5198909...".
        var digits = new string(search.Where(char.IsDigit).ToArray());
        var phonePattern = digits.Length > 0 ? $"%{digits}%" : null;

        return reservations.Where(r =>
            EF.Functions.ILike(r.Guest1.Name, pattern, @"\")
            || (r.Guest2 != null && EF.Functions.ILike(r.Guest2.Name, pattern, @"\"))
            || (phonePattern != null && EF.Functions.ILike(r.ContactPhone, phonePattern)));
    }

    // % e _ são curingas do LIKE: sem escapar, buscar "_" acharia qualquer coisa.
    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    // Lista fechada: o texto do usuário nunca vira nome de coluna. O Id no fim desempata linhas iguais
    // (sem ele, duas reservas com o mesmo check-in podem trocar de página entre uma requisição e outra).
    private static IQueryable<Reservation> ApplySort(IQueryable<Reservation> reservations, ReservationQuery query)
    {
        // Padrão do contrato: só confirmadas → por check-in; o resto → pelas mais recentes.
        var onlyConfirmed = query.Status is { Count: > 0 } s && s.All(x => x == ReservationStatus.Confirmed);
        var sort = query.Sort ?? (onlyConfirmed ? ReservationSort.CheckIn : ReservationSort.CreatedAt);
        var order = query.Order ?? (sort is ReservationSort.CreatedAt or ReservationSort.Total ? SortOrder.Desc : SortOrder.Asc);
        var desc = order == SortOrder.Desc;

        var sorted = sort switch
        {
            ReservationSort.CreatedAt => desc ? reservations.OrderByDescending(r => r.CreatedAt) : reservations.OrderBy(r => r.CreatedAt),
            ReservationSort.CheckIn => desc ? reservations.OrderByDescending(r => r.CheckIn) : reservations.OrderBy(r => r.CheckIn),
            ReservationSort.Total => desc ? reservations.OrderByDescending(r => r.Total) : reservations.OrderBy(r => r.Total),
            ReservationSort.GuestName => desc ? reservations.OrderByDescending(r => r.Guest1.Name) : reservations.OrderBy(r => r.Guest1.Name),
            _ => throw new ArgumentOutOfRangeException(nameof(query), sort, "Sort desconhecido (o ReservationQuery já devia ter barrado).")
        };

        return sorted.ThenBy(r => r.Id);
    }

    // Só referência: recalcula com os preços ATUAIS. Os valores que valem são os gravados na reserva (snapshot).
    private async Task<IReadOnlyList<NightBreakdownDto>> GetNightsBreakdown(Reservation reservation, CancellationToken ct)
    {
        var pricingRules = await db.PricingRules.AsNoTracking().ToListAsync(ct);

        // Pacotes que cruzam a estadia (mesmo que já tenham passado): é o que o PriceNight precisa.
        var packages = await db.SpecialPackages
            .AsNoTracking()
            .Where(p => p.CheckIn < reservation.CheckOut && reservation.CheckIn < p.CheckOut)
            .Select(p => new PackageInfo(p.Id, p.Name, p.CheckIn, p.CheckOut, p.Price))
            .ToListAsync(ct);

        // Bloqueios não entram no preço de uma noite, então vão vazios.
        var bookingData = new BookingData(
            pricingRules.FirstOrDefault(p => p.Kind == PricingKind.Weekday)?.PricePerNight,
            pricingRules.FirstOrDefault(p => p.Kind == PricingKind.Weekend)?.PricePerNight,
            WeekendMinNights: null,
            BlockedNights: new HashSet<DateOnly>(),
            Packages: packages);

        return BookingRules.Nights(reservation.CheckIn, reservation.CheckOut)
            .Select(night => BookingRules.PriceNight(night, bookingData))
            .OfType<NightPrice>() // sem preço cadastrado a noite fica de fora (não inventa valor)
            .Select(p => new NightBreakdownDto(p.Date, p.Kind, p.Price))
            .ToList();
    }

    private static GuestDetailDto ToGuestDto(Guest guest) =>
        new(guest.Id, guest.Name, guest.Cpf, guest.PhoneNumber, guest.Email);
}
