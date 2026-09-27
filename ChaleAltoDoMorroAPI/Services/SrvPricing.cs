using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Dtos;
using ChaleAltoDoMorroAPI.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Services;

public class SrvPricing(AppDbContext db)
{
    public async Task<List<PricingRuleDto>> GetAll(CancellationToken ct = default) =>
        await db.PricingRules
            .AsNoTracking()
            .OrderBy(r => r.Kind)
            .Select(r => new PricingRuleDto(r.Kind, r.Label, r.PricePerNight, r.MinNights))
            .ToListAsync(ct);

    public async Task<AdminResult<PricingRuleDto>> Get(PricingKind kind, CancellationToken ct = default)
    {
        var rule = await db.PricingRules.AsNoTracking().FirstOrDefaultAsync(r => r.Kind == kind, ct);

        return rule is null
            ? new NotFound<PricingRuleDto>($"Não há preço cadastrado para '{kind}'.")
            : new Success<PricingRuleDto>(ToDto(rule));
    }

    // PUT = "o preço deste tipo passa a ser este": atualiza ou cria (só existem 2 tipos, a chave é o kind).
    public async Task<AdminResult<PricingRuleDto>> Put(PricingKind kind, PutPricingRuleRequest request, CancellationToken ct = default)
    {
        // Mínimo de diárias só existe no fim de semana (REGRAS_RESERVA).
        if (kind == PricingKind.Weekday && request.MinNights is not null)
            return new Invalid<PricingRuleDto>("Mínimo de diárias só vale para o fim de semana.");

        var rule = await db.PricingRules.FirstOrDefaultAsync(r => r.Kind == kind, ct);

        if (rule is null)
        {
            rule = new PricingRule { Kind = kind };
            db.PricingRules.Add(rule);
        }

        rule.Label = request.Label;
        rule.PricePerNight = request.PricePerNight;
        rule.MinNights = request.MinNights;

        await db.SaveChangesAsync(ct);

        return new Success<PricingRuleDto>(ToDto(rule));
    }

    private static PricingRuleDto ToDto(PricingRule rule) => new(rule.Kind, rule.Label, rule.PricePerNight, rule.MinNights);
}
