using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CriptoPortfolio.Infrastructure.Persistence.Configurations;

public sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> b)
    {
        b.ToTable("Portfolios");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.UserId);

        // Al borrar un usuario (RGPD) se borran sus portafolios y, en cascada, todo lo suyo.
        b.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.Transactions)
            .WithOne()
            .HasForeignKey(t => t.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.ToTable("Transactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.Quantity).HasPrecision(28, 12);     // hasta 16 cifras enteras (SHIB, PEPE...) y 12 decimales
        b.Property(x => x.UnitPriceUsd).HasPrecision(28, 18); // precio unitario

        // Consulta típica: movimientos de un portafolio por moneda y fecha (FIFO, valoración diaria).
        b.HasIndex(x => new { x.PortfolioId, x.AssetId, x.DateUtc });

        b.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PortfolioDailyValueConfiguration : IEntityTypeConfiguration<PortfolioDailyValue>
{
    public void Configure(EntityTypeBuilder<PortfolioDailyValue> b)
    {
        b.ToTable("PortfolioDailyValues");
        b.HasKey(x => new { x.PortfolioId, x.DateUtc });

        b.HasOne<Portfolio>()
            .WithMany()
            .HasForeignKey(x => x.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
