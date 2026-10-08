using System.Linq.Expressions;
using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// Gestão das reservas no portal: listar, abrir, confirmar e cancelar (task 9), criar e editar (task 21).
// A pré-reserva pública continua no SrvReservation.
public class SrvAdminReservation(AppDbContext db, SrvAvailability srvAvailability, SrvSettings srvSettings, IClock brazilClock, TimeProvider timeProvider)
{
    // Um mapeamento só para a lista e para o detalhe. Como Expression, o EF traduz em SELECT com JOIN
    // (r.Guest1.Name vira JOIN em guests, sem Include). Compilado, serve para mapear a entidade já carregada.
    internal static readonly Expression<Func<Reservation, ReservationListItemDto>> ToListItem = r =>
        new ReservationListItemDto(
            r.Id, r.Status, r.CheckIn, r.CheckOut, r.Nights,
            r.Guest1.Name, r.Guest2 != null ? r.Guest2.Name : null, r.ContactPhone,
            r.PackageName, r.KitName, r.Total, r.IncludesColonialBreakfast,
            r.CreatedAt, r.ConfirmedAt, r.CancelledAt, r.BufferBeforeNights, r.BufferAfterNights, r.Source);

    private static readonly Func<Reservation, ReservationListItemDto> ToListItemCompiled = ToListItem.Compile();

    // ---------- Leitura (task 9) ----------

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
            CalculatedTotal = reservation.CalculatedTotal,
            PriceAdjusted = reservation.Total != reservation.CalculatedTotal,
            EstimatedTotalFromSite = reservation.EstimatedTotalFromSite,
            // Reserva do portal não tem total do site: nada a comparar.
            SiteTotalDiverged = reservation.EstimatedTotalFromSite is { } estimated && estimated != reservation.Total,
            // Sem AttributionSource gravado = não sabemos a origem: o objeto inteiro vai null.
            Attribution = reservation.AttributionSource is { } source
                ? new ReservationAttributionDto(source, reservation.UtmSource, reservation.UtmMedium,
                    reservation.UtmCampaign, reservation.UtmContent, reservation.UtmTerm, reservation.Fbclid,
                    reservation.FirstVisitAt)
                : null,
            NightsBreakdown = await GetNightsBreakdown(reservation, ct)
        });
    }

    // ---------- Status (task 9) ----------

    public async Task<AdminResult<ReservationDetailDto>> Confirm(Guid id, ConfirmReservationRequest? request, CancellationToken ct = default)
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

        // Folga efetiva (task 16): a do pedido ou, se não veio, a global. Ela vira o snapshot da reserva.
        var settings = await srvSettings.GetBookingSettings(ct);
        var before = request?.BufferBeforeNights ?? settings.BufferBeforeNights;
        var after = request?.BufferAfterNights ?? settings.BufferAfterNights;

        // Revalida: outra reserva pode ter sido confirmada nessas datas depois que esta chegou, e agora a folga
        // de cada lado também conta (regra 3). Esta reserva ainda está pendente, então não "bate com ela mesma".
        var stay = new BufferedStay(reservation.CheckIn, reservation.CheckOut, before, after);
        switch (await srvAvailability.ValidateConfirmation(stay, reservation.Id, ct))
        {
            case BlockedNight blocked:
                return new Conflict<ReservationDetailDto>($"Não dá para confirmar: a noite de {blocked.Night:dd/MM/yyyy} está bloqueada.");
            case ReservationConflict other:
                return new Conflict<ReservationDetailDto>(
                    $"Não dá para confirmar: com a folga entre hóspedes, as datas batem com a reserva confirmada de " +
                    $"{other.CheckIn:dd/MM/yyyy} a {other.CheckOut:dd/MM/yyyy}.");
        }

        reservation.TryConfirm(timeProvider.GetUtcNow());
        reservation.BufferBeforeNights = before;
        reservation.BufferAfterNights = after;
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

    // ---------- Criar e editar pelo portal (task 21) ----------

    public async Task<AdminResult<ReservationDetailDto>> Create(CreateReservationRequest request, CancellationToken ct = default)
    {
        // Mesmo cadeado da confirmação: uma reserva confirmada criada aqui ocupa noites do calendário.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await srvAvailability.LockCalendar(ct);

        var reservation = new Reservation
        {
            Source = ReservationSource.Portal,
            Status = ReservationStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow()
        };

        var problem = await ApplyForm(reservation, request, ct);
        if (problem is not null)
            return problem;

        // Nasce pendente e, se o admin pediu, passa pela MESMA transição da rota de confirmar.
        // A disponibilidade do ApplyForm já usou as noites publicadas, que contam a folga GLOBAL (regra 4):
        // então o snapshot de uma reserva criada confirmada é o global.
        if (request.Status == ReservationStatus.Confirmed)
        {
            var settings = await srvSettings.GetBookingSettings(ct);
            reservation.TryConfirm(reservation.CreatedAt);
            reservation.BufferBeforeNights = settings.BufferBeforeNights;
            reservation.BufferAfterNights = settings.BufferAfterNights;
        }

        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await Get(reservation.Id, ct);
    }

    // PUT: substitui o formulário inteiro. O status não muda por aqui (para isso há confirmar e cancelar).
    public async Task<AdminResult<ReservationDetailDto>> Update(Guid id, SaveReservationRequest request, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await srvAvailability.LockCalendar(ct);

        // Include: o ApplyForm precisa dos hóspedes atuais (para atualizar o hóspede 2 sem CPF, ou tirá-lo da reserva).
        var reservation = await db.Reservations
            .Include(r => r.Guest1)
            .Include(r => r.Guest2)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (reservation is null)
            return new NotFound<ReservationDetailDto>("Reserva não encontrada.");

        if (reservation.Status == ReservationStatus.Cancelled)
            return new Conflict<ReservationDetailDto>("Não dá para editar uma reserva cancelada.");

        var problem = await ApplyForm(reservation, request, ct);
        if (problem is not null)
            return problem;

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return await Get(id, ct);
    }

    /// <summary>
    /// Valida o formulário e preenche a reserva (nova ou existente). Devolve o erro, ou null se deu tudo certo.
    /// É o miolo comum do criar e do editar: as regras ficam num lugar só.
    /// </summary>
    private async Task<AdminResult<ReservationDetailDto>?> ApplyForm(Reservation reservation, SaveReservationRequest request, CancellationToken ct)
    {
        // 1. Datas: para o admin, só a disponibilidade. A própria reserva não conta como ocupação
        //    (senão editar só o telefone de uma confirmada daria 409). Numa reserva nova o Id ainda é vazio: não ignora nada.
        var unavailable = (await srvAvailability.GetUnavailableNights(ct, ignoreReservationId: reservation.Id)).ToHashSet();

        switch (AdminBookingRules.ValidateStay(request.CheckIn, request.CheckOut, unavailable))
        {
            case BlockedNight blocked:
                return new Conflict<ReservationDetailDto>($"A noite de {blocked.Night:dd/MM/yyyy} já está bloqueada ou com outra reserva confirmada.");
            case CheckOutNotAfterCheckIn:
                return new Invalid<ReservationDetailDto>("O check-out precisa ser depois do check-in.");
            case InvalidDate:
                return new Invalid<ReservationDetailDto>("Informe as datas de check-in e check-out.");
        }

        // 2. Kit: o admin pode usar um kit desativado (ex.: editar uma reserva antiga cujo kit saiu de linha).
        Kit? kit = null;
        if (request.KitId is { } kitId)
        {
            kit = await db.Kits.AsNoTracking().FirstOrDefaultAsync(k => k.Id == kitId, ct);
            if (kit is null)
                return new Invalid<ReservationDetailDto>("Kit não encontrado.");
        }

        // 3. Preço: calcula sempre (vira o CalculatedTotal) e usa o combinado se o admin informou.
        var pricingData = await LoadPricingData(request.CheckIn, request.CheckOut, ct);
        var kitInfo = kit is null ? null : new KitInfo(kit.Id, kit.Name, kit.Price);
        var quote = BookingRules.Quote(request.CheckIn, request.CheckOut, pricingData, kitInfo);
        var price = AdminBookingRules.Price(quote, request.Total, kit?.Price ?? 0m);

        if (price is null)
            return new Invalid<ReservationDetailDto>("Não há preço cadastrado para calcular esta estadia. Informe o total combinado.");

        // 4. Hóspedes: cadastra ou atualiza. Mesmo CPF nos dois = a mesma pessoa (igual à rota do site).
        var guest1 = await ResolveGuest(request.Guest1, reservation.Guest1, ct);
        Guest? guest2 = null;
        if (request.Guest2 is { } secondGuest)
        {
            guest2 = secondGuest.Cpf is not null && secondGuest.Cpf == guest1.Cpf
                ? guest1
                : await ResolveGuest(secondGuest, reservation.Guest2, ct);
        }

        // 5. Tudo validado: agora sim preenche a reserva.
        reservation.CheckIn = request.CheckIn;
        reservation.CheckOut = request.CheckOut;
        reservation.Nights = BookingRules.Nights(request.CheckIn, request.CheckOut).Count;
        reservation.IncludesColonialBreakfast = reservation.Nights >= BookingRules.ColonialBreakfastMinNights;

        // Pacote só quando as datas são exatamente as dele (é o que o Quote devolve em Package).
        reservation.PackageId = quote?.Package?.Id;
        reservation.PackageName = quote?.Package?.Name;

        reservation.KitId = kit?.Id;
        reservation.KitName = kit?.Name;
        reservation.KitPrice = kit?.Price;

        reservation.Total = price.Total;
        reservation.CalculatedTotal = price.CalculatedTotal;
        reservation.NightsTotal = price.NightsTotal;

        reservation.Guest1 = guest1;
        reservation.Guest2 = guest2; // null tira o hóspede 2 da reserva (o cadastro dele continua existindo)

        // Snapshot do contato. O SaveReservationRequest.Validate garante telefone no hóspede 1.
        reservation.ContactPhone = request.Guest1.Phone!;
        reservation.ContactEmail = request.Guest1.Email;

        reservation.AdminNotes = string.IsNullOrWhiteSpace(request.AdminNotes) ? null : request.AdminNotes.Trim();

        return null;
    }

    /// <summary>
    /// Acha ou cria o cadastro do hóspede e atualiza com o que veio do portal.
    /// Diferente da rota pública, aqui PODE atualizar um cadastro existente: quem chama é o admin logado.
    /// </summary>
    /// <param name="current">O hóspede que já estava nessa posição da reserva (null numa reserva nova).</param>
    private async Task<Guest> ResolveGuest(AdminGuestRequest request, Guest? current, CancellationToken ct)
    {
        Guest? guest = null;

        // Com CPF: o CPF identifica a pessoa entre reservas.
        if (request.Cpf is not null)
            guest = await db.Guests.FirstOrDefaultAsync(g => g.Cpf == request.Cpf, ct);

        // Hóspede sem CPF é um cadastro só desta reserva: ao editar, atualiza ele em vez de criar outro a cada PUT.
        if (guest is null && current is { Cpf: null })
            guest = current;

        if (guest is null)
        {
            guest = new Guest();
            db.Guests.Add(guest);
        }

        guest.Name = request.Name.Trim();
        guest.Cpf ??= request.Cpf;

        // Campo vazio no formulário não apaga o que já está no cadastro (o hóspede pode ter outras reservas).
        if (request.Phone is not null)
            guest.PhoneNumber = request.Phone;
        if (request.Email is not null)
            guest.Email = request.Email;

        return guest;
    }

    // ---------- Apoio ----------

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
    internal static string EscapeLike(string text) =>
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
        var bookingData = await LoadPricingData(reservation.CheckIn, reservation.CheckOut, ct);

        return BookingRules.Nights(reservation.CheckIn, reservation.CheckOut)
            .Select(night => BookingRules.PriceNight(night, bookingData))
            .OfType<NightPrice>() // sem preço cadastrado a noite fica de fora (não inventa valor)
            .Select(p => new NightBreakdownDto(p.Date, p.Kind, p.Price))
            .ToList();
    }

    /// <summary>
    /// Só o que o cálculo de PREÇO precisa: preços atuais e os pacotes que cruzam a estadia (mesmo já passados).
    /// Bloqueios não mudam o preço de uma noite, então vão vazios (a disponibilidade é checada à parte).
    /// </summary>
    private async Task<BookingData> LoadPricingData(DateOnly checkIn, DateOnly checkOut, CancellationToken ct)
    {
        var pricingRules = await db.PricingRules.AsNoTracking().ToListAsync(ct);

        var packages = await db.SpecialPackages
            .AsNoTracking()
            .Where(p => p.CheckIn < checkOut && checkIn < p.CheckOut)
            .Select(p => new PackageInfo(p.Id, p.Name, p.CheckIn, p.CheckOut, p.Price))
            .ToListAsync(ct);

        return new BookingData(
            pricingRules.FirstOrDefault(p => p.Kind == PricingKind.Weekday)?.PricePerNight,
            pricingRules.FirstOrDefault(p => p.Kind == PricingKind.Weekend)?.PricePerNight,
            WeekendMinNights: null,
            BlockedNights: new HashSet<DateOnly>(),
            Packages: packages);
    }

    private static GuestReservationDetailDto ToGuestDto(Guest guest) =>
        new(guest.Id, guest.Name, guest.Cpf, guest.PhoneNumber, guest.Email);
}
