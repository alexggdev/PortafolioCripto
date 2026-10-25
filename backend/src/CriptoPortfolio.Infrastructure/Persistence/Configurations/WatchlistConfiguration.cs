using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CriptoPortfolio.Infrastructure.Persistence.Configurations;

public sealed class WatchlistItemConfiguration : IEntityTypeConfiguration<WatchlistItem>
{
    public void Configure(EntityTypeBuilder<WatchlistItem> b)
    {
        b.ToTable("WatchlistItems");
        b.HasKey(x => new { x.UserId, x.AssetId });

        // Al borrar el usuario (RGPD) desaparece su lista.
        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: las monedas no se borran (solo se desactivan).
        b.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
