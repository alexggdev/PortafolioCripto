using CriptoPortfolio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CriptoPortfolio.Infrastructure.Persistence.Configurations;

public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        b.ToTable("Assets");
        b.HasKey(x => x.Id);

        b.Property(x => x.Symbol).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsRequired();
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.CoinGeckoId).HasMaxLength(200);
        b.Property(x => x.BinanceSymbol).HasMaxLength(32);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.CoinGeckoId).IsUnique().HasFilter("[CoinGeckoId] IS NOT NULL");
        b.HasIndex(x => x.BinanceSymbol).IsUnique().HasFilter("[BinanceSymbol] IS NOT NULL");
        b.HasIndex(x => new { x.IsActive, x.Rank });
        b.HasIndex(x => x.Symbol); // el símbolo NO es único: varias monedas comparten ticker

        b.HasOne(x => x.Price)
            .WithOne()
            .HasForeignKey<AssetPrice>(p => p.AssetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AssetPriceConfiguration : IEntityTypeConfiguration<AssetPrice>
{
    public void Configure(EntityTypeBuilder<AssetPrice> b)
    {
        b.ToTable("AssetPrices");
        b.HasKey(x => x.AssetId);
        b.Property(x => x.AssetId).ValueGeneratedNever(); // es PK y FK a la vez: nunca identity

        b.Property(x => x.PriceUsd).HasPrecision(28, 18); // precio unitario: hasta 10 cifras enteras y 18 decimales
        b.Property(x => x.Change1hPct).HasPrecision(18, 6);
        b.Property(x => x.Change24hPct).HasPrecision(18, 6);
        b.Property(x => x.Change7dPct).HasPrecision(18, 6);
    }
}

public sealed class AssetDailyPriceConfiguration : IEntityTypeConfiguration<AssetDailyPrice>
{
    public void Configure(EntityTypeBuilder<AssetDailyPrice> b)
    {
        b.ToTable("AssetDailyPrices");
        b.HasKey(x => new { x.AssetId, x.DateUtc });

        b.Property(x => x.CloseUsd).HasPrecision(28, 18);
        b.Property(x => x.OpenUsd).HasPrecision(28, 18);
        b.Property(x => x.HighUsd).HasPrecision(28, 18);
        b.Property(x => x.LowUsd).HasPrecision(28, 18);

        b.HasOne<Asset>()
            .WithMany()
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.Restrict); // las monedas no se borran
    }
}
