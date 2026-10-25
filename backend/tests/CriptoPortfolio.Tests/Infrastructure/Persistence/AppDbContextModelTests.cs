using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Tests.Infrastructure.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Persistence;

public class AppDbContextModelTests : IDisposable
{
    private static readonly DateTime Stamp = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteTestDb _db = new();

    public void Dispose() => _db.Dispose();

    private static Asset NewAsset(string slug, string? coinGeckoId = null) => new()
    {
        Symbol = slug.ToUpperInvariant(),
        Name = slug,
        Slug = slug,
        CoinGeckoId = coinGeckoId,
        CreatedAtUtc = Stamp,
        UpdatedAtUtc = Stamp
    };

    [Fact]
    public async Task Price_keeps_all_18_decimals_and_amounts_keep_8()
    {
        const decimal tiny = 0.000000012345678901m;

        await using (var write = _db.CreateContext())
        {
            var asset = NewAsset("tiny-coin", "tiny-coin");
            asset.Price = new AssetPrice
            {
                PriceUsd = tiny,
                MarketCapUsd = 2_878_000_000_000.12345678m,
                Change24hPct = -3.141592m,
                Source = DataSource.CoinGecko,
                UpdatedAtUtc = Stamp
            };
            write.Assets.Add(asset);
            await write.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var loaded = await read.Assets.Include(a => a.Price).SingleAsync(a => a.Slug == "tiny-coin");

        Assert.Equal(tiny, loaded.Price!.PriceUsd);
        Assert.Equal(2_878_000_000_000.12345678m, loaded.Price.MarketCapUsd);
        Assert.Equal(-3.141592m, loaded.Price.Change24hPct);
    }

    // SQLite no se queja de los decimales demasiado anchos, pero SqlClient sí ("Conversion overflows") cuando el
    // valor, ya pasado a la escala de la columna, no cabe en un System.Decimal (unos 28 dígitos). Con
    // decimal(38,18) la capitalización de Bitcoin ya fallaba. Esta prueba vigila el modelo, no los datos.
    [Fact]
    public void Every_decimal_column_has_at_most_28_digits_and_room_for_realistic_values()
    {
        using var context = _db.CreateContext();

        var columns = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(decimal))
            .ToList();

        Assert.NotEmpty(columns);
        foreach (var column in columns)
        {
            var name = column.DeclaringType.Name + "." + column.Name;
            var precision = column.GetPrecision();
            var scale = column.GetScale();

            Assert.True(precision is > 0 and <= 28, $"{name}: precisión {precision}, máximo 28");
            Assert.True(scale is not null, $"{name}: sin escala configurada");
        }

        int IntegerDigits(Type type, string property)
        {
            var p = context.Model.FindEntityType(type)!.FindProperty(property)!;
            return p.GetPrecision()!.Value - p.GetScale()!.Value;
        }

        // Cifras enteras disponibles: capitalización de Bitcoin = 13, cantidad de SHIB = 12-13.
        Assert.True(IntegerDigits(typeof(AssetPrice), nameof(AssetPrice.MarketCapUsd)) >= 13);
        Assert.True(IntegerDigits(typeof(AssetPrice), nameof(AssetPrice.PriceUsd)) >= 7);
        Assert.True(IntegerDigits(typeof(Transaction), nameof(Transaction.Quantity)) >= 13);
    }

    [Fact]
    public async Task Dates_are_read_back_as_utc()
    {
        await using (var write = _db.CreateContext())
        {
            write.Assets.Add(NewAsset("bitcoin"));
            await write.SaveChangesAsync();
        }

        await using var read = _db.CreateContext();
        var loaded = await read.Assets.SingleAsync();

        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAtUtc.Kind);
        Assert.Equal(Stamp, loaded.CreatedAtUtc);
    }

    [Fact]
    public async Task Duplicate_slug_is_rejected()
    {
        await using (var first = _db.CreateContext())
        {
            first.Assets.Add(NewAsset("bitcoin", "bitcoin"));
            await first.SaveChangesAsync();
        }

        await using var second = _db.CreateContext();
        second.Assets.Add(NewAsset("bitcoin", "otro-id"));

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_coingecko_id_is_rejected()
    {
        await using (var first = _db.CreateContext())
        {
            first.Assets.Add(NewAsset("bitcoin", "bitcoin"));
            await first.SaveChangesAsync();
        }

        await using var second = _db.CreateContext();
        second.Assets.Add(NewAsset("bitcoin-2", "bitcoin"));

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Several_assets_without_coingecko_id_can_coexist()
    {
        await using var context = _db.CreateContext();
        context.Assets.Add(NewAsset("a"));
        context.Assets.Add(NewAsset("b"));
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.Assets.CountAsync());
    }

    [Fact]
    public async Task Daily_price_key_is_asset_plus_date()
    {
        int assetId;
        await using (var setup = _db.CreateContext())
        {
            var asset = NewAsset("bitcoin");
            setup.Assets.Add(asset);
            await setup.SaveChangesAsync();
            assetId = asset.Id;

            setup.AssetDailyPrices.Add(new AssetDailyPrice
            {
                AssetId = assetId,
                DateUtc = new DateOnly(2026, 10, 9),
                CloseUsd = 100_000m,
                Source = DataSource.Binance
            });
            await setup.SaveChangesAsync();
        }

        await using var duplicate = _db.CreateContext();
        duplicate.AssetDailyPrices.Add(new AssetDailyPrice
        {
            AssetId = assetId,
            DateUtc = new DateOnly(2026, 10, 9),
            CloseUsd = 101_000m,
            Source = DataSource.Binance
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    public async Task Daily_price_for_a_missing_asset_is_rejected()
    {
        await using var context = _db.CreateContext();
        context.AssetDailyPrices.Add(new AssetDailyPrice
        {
            AssetId = 9999,
            DateUtc = new DateOnly(2026, 10, 9),
            CloseUsd = 1m,
            Source = DataSource.CoinGecko
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
