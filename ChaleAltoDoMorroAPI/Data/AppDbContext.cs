using ChaleAltoDoMorroAPI.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChaleAltoDoMorroAPI.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();
    public DbSet<BlockedDate> BlockedDates => Set<BlockedDate>();
    public DbSet<SpecialPackage> SpecialPackages => Set<SpecialPackage>();
    public DbSet<Kit> Kits => Set<Kit>();
    public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
    public DbSet<TourVideo> TourVideos => Set<TourVideo>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Primeiro o Identity (tabelas de usuários), depois as nossas configurações.
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
