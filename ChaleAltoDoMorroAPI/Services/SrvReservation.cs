using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

public class SrvReservation(AppDbContext db, IClock brazilClock, TimeProvider timeProvider, ILogger<SrvReservation> log)
{

    public async Task<PreReservationResponse> CreatePreReservation(PostPreReservationRequest request, CancellationToken ct = default)
    {
        var bookingData = await GetBookingData();

        //Valida de acordo com as regras do banco o pedido de pré-reserva
        var validateRequestByBookingRules = BookingRules.ValidateStay(request.CheckIn, request.CheckOut, bookingData, brazilClock.Today());

        if (validateRequestByBookingRules != null)
            return new StayRejected(validateRequestByBookingRules);

        var validateRequestPackageResponse = ValidateRequestPackage(request, bookingData);

        if (validateRequestPackageResponse.ErrorResponse != null)
            return validateRequestPackageResponse.ErrorResponse;

        var validateRequestKitResponse = await ValidateRequestKit(request);

        if (validateRequestKitResponse.ErrorResponse != null)
            return validateRequestKitResponse.ErrorResponse;

        var kit = validateRequestKitResponse.Kit;
        var kitInfo = kit is null ? null : new KitInfo(kit.Id, kit.Name, kit.Price);

        // O preço é SEMPRE o recalculado aqui; o estimatedTotal do site é só para comparação.
        var requestPrices = BookingRules.Quote(request.CheckIn, request.CheckOut, bookingData, kitInfo);

        if (requestPrices is null)
            return new PricingNotConfigured();

        var mainGuest = await GetOrAddGuest(request.Guest.Name, request.Guest.Cpf, request.Guest.Phone, request.Guest.Email);
        Guest? secondGuest = null;

        var requestSecondGuest = request.SecondGuest;
        if (requestSecondGuest != null && !string.IsNullOrWhiteSpace(requestSecondGuest.Name))
        {
            // Mesmo CPF dos dois hóspedes: reaproveita o cadastro (o hóspede 1 pode ainda não estar salvo no banco).
            secondGuest = requestSecondGuest.Cpf != null && requestSecondGuest.Cpf == mainGuest.Cpf
                ? mainGuest
                : await GetOrAddGuest(requestSecondGuest.Name, requestSecondGuest.Cpf, phone: null, email: null);
        }

        var reservationId = await AddReservation(requestPrices, validateRequestPackageResponse.package, kit, mainGuest, secondGuest, request);

        if (request.EstimatedTotal is { } estimatedTotal && estimatedTotal != requestPrices.Total)
        {
            // Sem dados pessoais no log: só o id da reserva e os valores.
            log.LogWarning(
                "Pré-reserva {ReservationId}: total estimado pelo site {EstimatedTotal} difere do recalculado {Total}.",
                reservationId, estimatedTotal, requestPrices.Total);
        }

        return new Created(reservationId, requestPrices.Total);
    }

    private async Task<Guid> AddReservation(StayQuote bookingQuote, PackageInfo? package, Kit? kit, Guest mainGuest, Guest? secondGuest, PostPreReservationRequest request)
    {
        Reservation reservation = new()
        {
            CheckIn = request.CheckIn,
            CheckOut = request.CheckOut,
            PackageId = package?.Id,
            PackageName = package?.Name,
            KitId = kit?.Id,
            KitName = kit?.Name,
            KitPrice = kit?.Price,
            // Só as navegações: o EF preenche Guest1Id/Guest2Id (e insere o hóspede se ele for novo).
            Guest1 = mainGuest,
            Guest2 = secondGuest,
            EstimatedTotalFromSite = request.EstimatedTotal ?? 0m,
            Total = bookingQuote.Total,
            // Calculado no servidor, nunca o request.Nights (que vem do navegador).
            Nights = bookingQuote.Nights.Count,
            NightsTotal = bookingQuote.NightsTotal,
            IncludesColonialBreakfast = bookingQuote.IncludesColonialBreakfast,
            // Contato digitado NESTE pedido: o cadastro de um hóspede existente não é alterado pela rota pública.
            ContactEmail = request.Guest.Email,
            ContactPhone = request.Guest.Phone,
            Observations = request.Guest.Notes,
            Status = ReservationStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow()
        };

        await db.Reservations.AddAsync(reservation);

        await db.SaveChangesAsync();

        return reservation.Id;
    }

    // CPF já cadastrado: devolve o cadastro existente SEM alterar (segurança: a rota é pública).
    // Sem CPF (hóspede 2 opcional): não há como deduplicar, então cria um cadastro novo.
    private async Task<Guest> GetOrAddGuest(string name, string? cpf, string? phone, string? email)
    {
        if (cpf != null)
        {
            // Com tracking: o EF precisa saber que este hóspede já existe, senão tenta inserir de novo.
            var guestFromDb = await db.Guests.FirstOrDefaultAsync(g => g.Cpf == cpf);

            if (guestFromDb != null)
                return guestFromDb;
        }

        Guest addGuest = new()
        {
            Cpf = cpf,
            Name = name.Trim(),
            PhoneNumber = phone,
            Email = email
        };

        await db.Guests.AddAsync(addGuest);

        return addGuest;
    }

    //Regras/configurações de hospedagem do banco
    private async Task<BookingData> GetBookingData()
    {
        var today = brazilClock.Today();

        var pricingRules = await db.PricingRules.AsNoTracking().ToListAsync();
        var packageInfo = await db.SpecialPackages.AsNoTracking().Where(p => p.CheckOut > today).Select(p => new PackageInfo(p.Id, p.Name, p.CheckIn, p.CheckOut, p.Price)).ToListAsync();
        // >= : o bloqueio de HOJE à noite também conta.
        var blockedNights = await db.BlockedDates.AsNoTracking().Where(b => b.Date >= today).Select(b => b.Date).ToHashSetAsync();

        // Regra ausente = null: o BookingRules já trata (sem weekend usa o preço de semana; sem weekday não orça).
        var weekdayRule = pricingRules.FirstOrDefault(p => p.Kind == PricingKind.Weekday);
        var weekendRule = pricingRules.FirstOrDefault(p => p.Kind == PricingKind.Weekend);

        return new BookingData(weekdayRule?.PricePerNight, weekendRule?.PricePerNight, weekendRule?.MinNights, blockedNights, packageInfo);
    }

    private static (PreReservationResponse? ErrorResponse, PackageInfo? package) ValidateRequestPackage(PostPreReservationRequest request, BookingData bookingData)
    {
        var packages = bookingData.Packages.ToList();

        PackageInfo? requestPackage = packages.Where(p => p.CheckIn == request.CheckIn && p.CheckOut == request.CheckOut).FirstOrDefault();

        if (requestPackage != null)
        {
            if (request.PackageId != requestPackage.Id)
                return (new PackageMismatch(request.CheckIn, request.CheckOut, requestPackage), null);
        }
        else
            if (request.PackageId != null)
                return (new PackageMismatch(request.CheckIn, request.CheckOut, requestPackage), null);

        return (null, requestPackage);
    }

    private async Task<(PreReservationResponse? ErrorResponse, Kit? Kit)> ValidateRequestKit(PostPreReservationRequest request)
    {
        if (!request.ExtraIds.Any())
            return (null, null);

        var requestKitId = request.ExtraIds[0];

        // Só kit ATIVO: um kit desativado no portal não pode mais ser vendido.
        var requestKit = await db.Kits.AsNoTracking().Where(k => k.Id == requestKitId && k.IsActive).FirstOrDefaultAsync();

        if (requestKit == null)
            return (new KitNotFound(), null);


        return (null, requestKit);
    }
}
