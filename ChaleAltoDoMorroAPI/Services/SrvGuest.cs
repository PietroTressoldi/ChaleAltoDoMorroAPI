using System.Linq.Expressions;
using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

// Tela "Hóspedes" do portal (task 17): lista com números calculados, detalhe com histórico e edição do contato.
public class SrvGuest(AppDbContext db, IClock brazilClock)
{
    public async Task<Paged<GuestListItemDto>> List(GuestQuery query, CancellationToken ct = default)
    {
        IQueryable<Guest> guests = db.Guests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
            guests = ApplySearch(guests, query.Search.Trim());

        var totalCount = await guests.CountAsync(ct);

        // Projeta ANTES de ordenar: assim dá para ordenar por Stays/TotalSpent/LastStay, que só existem na projeção.
        var items = await ApplySort(guests.Select(ToListItem(brazilClock.Today())), query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new Paged<GuestListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public async Task<AdminResult<GuestDetailDto>> Get(Guid id, CancellationToken ct = default)
    {
        var guest = await db.Guests.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guest is null)
            return new NotFound<GuestDetailDto>("Hóspede não encontrado.");

        // Mesmos números da lista (mesma expressão): a lista e o detalhe nunca discordam.
        var summary = await db.Guests.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(ToListItem(brazilClock.Today()))
            .FirstAsync(ct);

        // Histórico: TODAS as reservas, qualquer status, da mais recente para a mais antiga.
        var reservations = await db.Reservations.AsNoTracking()
            .Where(r => r.Guest1Id == id || r.Guest2Id == id)
            .OrderByDescending(r => r.CheckIn)
            .Select(SrvAdminReservation.ToListItem)
            .ToListAsync(ct);

        return new Success<GuestDetailDto>(new GuestDetailDto(summary)
        {
            Cpf = guest.Cpf,
            Notes = guest.Notes,
            Address = guest.Address,
            SocialMedia = guest.SocialMedia,
            Reservations = reservations
        });
    }

    public async Task<AdminResult<GuestDetailDto>> Update(Guid id, UpdateGuestRequest request, CancellationToken ct = default)
    {
        var guest = await db.Guests.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (guest is null)
            return new NotFound<GuestDetailDto>("Hóspede não encontrado.");

        // Muda só o cadastro. O ContactPhone/ContactEmail gravado em cada reserva é uma "foto" do momento e fica como está.
        guest.Name = request.Name.Trim();
        guest.PhoneNumber = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone;
        guest.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        guest.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        guest.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        guest.SocialMedia = string.IsNullOrWhiteSpace(request.SocialMedia) ? null : request.SocialMedia.Trim();

        await db.SaveChangesAsync(ct);

        return await Get(id, ct);
    }

    // Os números do hóspede, contando só reservas CONFIRMADAS em que ele é hóspede 1 ou 2.
    // Cada campo vira uma subconsulta dentro do MESMO SELECT (nada de uma consulta por hóspede).
    private Expression<Func<Guest, GuestListItemDto>> ToListItem(DateOnly today)
    {
        var confirmed = db.Reservations.Where(r => r.Status == ReservationStatus.Confirmed);

        return g => new GuestListItemDto
        {
            Id = g.Id,
            Name = g.Name,
            Phone = g.PhoneNumber,
            Email = g.Email,
            Stays = confirmed.Count(r => r.Guest1Id == g.Id || r.Guest2Id == g.Id),
            // SUM/MAX/MIN de nenhuma linha dá NULL no SQL: o "?" no cast deixa o C# receber esse null.
            TotalSpent = confirmed
                .Where(r => r.Guest1Id == g.Id || r.Guest2Id == g.Id)
                .Sum(r => (decimal?)r.Total) ?? 0,
            LastStay = confirmed
                .Where(r => (r.Guest1Id == g.Id || r.Guest2Id == g.Id) && r.CheckIn < today)
                .Max(r => (DateOnly?)r.CheckIn),
            NextStay = confirmed
                .Where(r => (r.Guest1Id == g.Id || r.Guest2Id == g.Id) && r.CheckIn >= today)
                .Min(r => (DateOnly?)r.CheckIn)
        };
    }

    private static IQueryable<Guest> ApplySearch(IQueryable<Guest> guests, string search)
    {
        var pattern = $"%{SrvAdminReservation.EscapeLike(search)}%";

        // CPF e telefone ficam gravados só com dígitos: "529.98" também acha "52998224725".
        var digits = new string(search.Where(char.IsDigit).ToArray());
        var digitsPattern = digits.Length > 0 ? $"%{digits}%" : null;

        return guests.Where(g =>
            EF.Functions.ILike(g.Name, pattern, @"\")
            || (g.Email != null && EF.Functions.ILike(g.Email, pattern, @"\"))
            || (digitsPattern != null && g.PhoneNumber != null && EF.Functions.ILike(g.PhoneNumber, digitsPattern))
            || (digitsPattern != null && g.Cpf != null && EF.Functions.ILike(g.Cpf, digitsPattern)));
    }

    private static IQueryable<GuestListItemDto> ApplySort(IQueryable<GuestListItemDto> guests, GuestQuery query)
    {
        var sort = query.Sort ?? GuestSort.Name;
        // Sem "order": nome de A a Z; números e datas do maior para o menor (quem mais veio/gastou no topo).
        var desc = (query.Order ?? (sort == GuestSort.Name ? SortOrder.Asc : SortOrder.Desc)) == SortOrder.Desc;

        var ordered = (sort, desc) switch
        {
            (GuestSort.Stays, false) => guests.OrderBy(g => g.Stays),
            (GuestSort.Stays, true) => guests.OrderByDescending(g => g.Stays),
            (GuestSort.TotalSpent, false) => guests.OrderBy(g => g.TotalSpent),
            (GuestSort.TotalSpent, true) => guests.OrderByDescending(g => g.TotalSpent),
            // No Postgres, NULL vem PRIMEIRO no DESC: quem nunca veio subiria para o topo. Então "sem data" vai para o fim.
            (GuestSort.LastStay, false) => guests.OrderBy(g => g.LastStay == null).ThenBy(g => g.LastStay),
            (GuestSort.LastStay, true) => guests.OrderBy(g => g.LastStay == null).ThenByDescending(g => g.LastStay),
            (_, false) => guests.OrderBy(g => g.Name),
            (_, true) => guests.OrderByDescending(g => g.Name)
        };

        // Desempate estável: sem isso, dois hóspedes empatados podem trocar de página entre uma chamada e outra.
        return ordered.ThenBy(g => g.Name).ThenBy(g => g.Id);
    }
}