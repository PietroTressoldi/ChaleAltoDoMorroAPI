using ChaleAltoDoMorroAPI.Domain;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Data;


//cria dados iniciais na tabela
public class DevSeeder(AppDbContext db, ILogger log, IClock clock, CancellationToken ct = default)
{

    public async Task SeedAsync()
    {

        try
        {
            await SeedPricing();

            await SeedBlockedDates();

            await SeedReservationKits();

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falha no seed de desenvolvimento");
            throw;
        }
    }

    private async Task SeedPricing()
    {

        if (await db.PricingRules.AnyAsync())
            return;

        PricingRule weekdayPricingRule = new()
        {
            Label = "Diária durante a semana",
            Kind = PricingKind.Weekday,
            MinNights = null,
            PricePerNight = 790,
            LongStayPricePerNight = 645
        };

        PricingRule weekendPricingRule = new()
        {
            Label = "Fim de semana (sex, sáb e dom)",
            Kind = PricingKind.Weekend,
            MinNights = 2,
            PricePerNight = 894.5M
        };

        PricingRule extendedPricingRule = new()
        {
            Label = "Diária estendida (sex → sáb ou sáb → dom)",
            Kind = PricingKind.Extended,
            PricePerNight = 1190
        };

        await db.PricingRules.AddAsync(weekdayPricingRule, ct);
        await db.PricingRules.AddAsync(weekendPricingRule, ct);
        await db.PricingRules.AddAsync(extendedPricingRule, ct);

    }

    private async Task SeedBlockedDates()
    {

        if (await db.BlockedDates.AnyAsync())
            return;

        BlockedDate blockedDate1 = new()
        {
            Date = clock.Today(),
            Reason = "Manutenção"
        };
        BlockedDate blockedDate2 = new()
        {
            Date = clock.Today().AddDays(1),
            Reason = "Manutenção"
        };
        BlockedDate blockedDate3 = new()
        {
            Date = clock.Today().AddDays(2),
            Reason = "Manutenção"
        };
        BlockedDate blockedDate4 = new()
        {
            Date = clock.Today().AddDays(11),
            Reason = "Manutenção"
        };

        await db.AddAsync(blockedDate1);
        await db.AddAsync(blockedDate2);
        await db.AddAsync(blockedDate3);
        await db.AddAsync(blockedDate4);

    }

    private async Task SeedReservationKits()
    {

        if (await db.Kits.AnyAsync())
            return;

        Kit kit1 = new()
        {
            Name = "Kit Toque de Romance",
            Price = 179,
            IsActive = true,
            Description = "",
            Items = new()
                {
                    "Espumante nacional Brut ou Moscatel (1 garrafa)",
                    "Pétalas de rosa",
                    "Velas decorativas",
                    "Balões de coração"
                },
            Order = 1
        };

        Kit kit2 = new()
        {
            Name = "Kit Eu Te Amo",
            Price = 279,
            IsActive = true,
            Description = "",
            Items = new()
                {
                    "Espumante nacional Brut ou Moscatel (1 garrafa)",
                    "Pétalas de rosa",
                    "Velas decorativas",
                    "Balões com a frase \"Te amo\""
                },
            Order = 2
        };

        Kit kit3 = new()
        {
            Name = "Kit Noite Especial",
            Price = 390,
            IsActive = true,
            Description = "",
            Items = new()
                {
                    "Arco de coração com rosas",
                    "Espumante nacional Brut ou Moscatel (1 garrafa)",
                    "Pétalas de rosa",
                    "Velas decorativas",
                    "Balões de coração",
                    "Chocolates",
                    "Frase com o pedido \"Casa Comigo\" ou \"Namora Comigo\""
                },
            Order = 3
        };

        await db.AddAsync(kit1);
        await db.AddAsync(kit2);
        await db.AddAsync(kit3);


    }

}
