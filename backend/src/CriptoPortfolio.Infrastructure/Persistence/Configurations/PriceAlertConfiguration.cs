using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CriptoPortfolio.Infrastructure.Persistence.Configurations;

public sealed class PriceAlertConfiguration : IEntityTypeConfiguration<PriceAlert>
{
    public void Configure(EntityTypeBuilder<PriceAlert> b)
    {
        b.ToTable("PriceAlerts");
        b.HasKey(x => x.Id);

        b.Property(x => x.TargetPriceUsd).HasPrecision(28, 18);
        b.Property(x => x.TriggeredPriceUsd).HasPrecision(28, 18);

        // Listado del usuario.
        b.HasIndex(x => new { x.UserId, x.CreatedAtUtc });

        // El worker solo lee las alertas activas: índice filtrado, pequeño aunque haya muchas ya disparadas.
        b.HasIndex(x => x.AssetId).HasFilter("[TriggeredAtUtc] IS NULL");

        // Al borrar el usuario (RGPD) desaparecen sus alertas.
        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Las monedas no se borran (solo se desactivan).
        b.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
