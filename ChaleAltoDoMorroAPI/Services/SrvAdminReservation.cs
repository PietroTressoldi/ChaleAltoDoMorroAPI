using System.Linq.Expressions;
using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// Gestão das reservas no portal (task 9). A criação pública continua no SrvReservation.
public class SrvAdminReservation(AppDbContext db, SrvAvailability srvAvailability, IClock brazilClock, TimeProvider timeProvider)
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
            AdminNotes = reservation.AdminNotes,
            PackageId = reservation.PackageId,
            KitId = reservation.KitId,
            KitPrice = reservation.KitPrice,
            NightsTotal = reservation.NightsTotal,
            EstimatedTotalFromSite = reservation.EstimatedTotalFromSite,
            SiteTotalDiverged = reservation.EstimatedTotalFromSite != reservation.Total,
            NightsBreakdown = await GetNightsBreakdown(reservation, ct)
        });
    }

    public async Task<AdminResult<ReservationDetailDto>> Confirm(Guid id, CancellationToken ct = default)
    {
        // Dois cliques ao mesmo tempo em reservas com a mesma noite: sem o cadeado, os dois leriam "livre" antes de
        // qualquer um gravar, e as duas seriam confirmadas. Com ele, o segundo espera o primeiro terminar e aí lê de novo.
        // Qualquer "return" antes do CommitAsync desfaz a transação (o "await using" chama Dispose → rollback).
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await srvAvailability.LockCalendar(ct);

        // Lida DEPOIS do cadeado: se alguém cancelou esta reserva enquanto esperávamos, já enxergamos.
        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (reservation is null)
            return new NotFound<ReservationDetailDto>("Reserva não encontrada.");

        if (reservation.Status != ReservationStatus.Pending)
            return new Conflict<ReservationDetailDto>($"Só dá para confirmar uma reserva pendente. Esta está {StatusText(reservation.Status)}.");

        // Revalida: outra reserva pode ter sido confirmada nessas datas depois que esta chegou.
        // Esta reserva ainda está pendente, então as noites dela não entram na conta (não "bate com ela mesma").
        var unavailable = (await srvAvailability.GetUnavailableNights(ct)).ToHashSet();
        var takenNights = BookingRules.Nights(reservation.CheckIn, reservation.CheckOut)
            .Where(unavailable.Contains)
            .ToList();

        if (takenNights.Count > 0)
            return new Conflict<ReservationDetailDto>(
                $"Não dá para confirmar: a(s) noite(s) {string.Join(", ", takenNights.Select(n => n.ToString("dd/MM/yyyy")))} " +
                "já está(ão) bloqueada(s) ou com outra reserva confirmada.");

        reservation.TryConfirm(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct); // solta o cadeado

        return await Get(id, ct);
    }

    // Cancelar libera as noites sozinho: a disponibilidade só conta reservas confirmadas.
    public async Task<AdminResult<ReservationDetailDto>> Cancel(Guid id, CancellationToken ct = default)
    {
        // Mesmo cadeado do Confirm: sem ele, "cancelar" e "confirmar" a MESMA reserva ao mesmo tempo poderiam
        // terminar com o confirmar gravando por cima do cancelar (reserva cancelada que volta a ocupar as noites).
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await srvAvailability.LockCalendar(ct);

        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (reservation is null)
            return new NotFound<ReservationDetailDto>("Reserva não encontrada.");

        if (!reservation.TryCancel(timeProvider.GetUtcNow()))
            return new Conflict<ReservationDetailDto>("Esta reserva já está cancelada.");

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await Get(id, ct);
    }

    public async Task<AdminResult<ReservationDetailDto>> UpdateNotes(Guid id, UpdateReservationNotesRequest request, CancellationToken ct = default)
    {
        var reservation = await db.Reservations.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (reservation is null)
            return new NotFound<ReservationDetailDto>("Reserva não encontrada.");

        reservation.AdminNotes = string.IsNullOrWhiteSpace(request.AdminNotes) ? null : request.AdminNotes.Trim();
        await db.SaveChangesAsync(ct);

        return await Get(id, ct);
    }

    private static string StatusText(ReservationStatus status) => status switch
    {
        ReservationStatus.Pending => "pendente",
        ReservationStatus.Confirmed => "confirmada",
        _ => "cancelada"
    };

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
