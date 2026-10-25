using CriptoPortfolio.Application.Watchlist;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Infrastructure.Catalog;
using CriptoPortfolio.Infrastructure.Identity;
using CriptoPortfolio.Infrastructure.Persistence;
using CriptoPortfolio.Infrastructure.Prices;
using CriptoPortfolio.Infrastructure.Watchlist;
using CriptoPortfolio.Tests.Infrastructure.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Watchlist;

public class WatchlistServiceTests : IDisposable
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly LivePriceCache _cache = new();

    public WatchlistServiceTests()
    {
        using var context = _db.CreateContext();
        foreach (var (id, name) in new[] { (Alice, "alice"), (Bob, "bob") })
        {
            context.Users.Add(new ApplicationUser
            {
                Id = id,
                UserName = name,
                NormalizedUserName = name.ToUpperInvariant(),
                Email = $"{name}@example.test",
                NormalizedEmail = $"{name.ToUpperInvariant()}@EXAMPLE.TEST",
                CreatedAtUtc = Now
            });
        }

        foreach (var (slug, symbol, active) in new[] { ("bitcoin", "BTC", true), ("ethereum", "ETH", true), ("muerta", "DEAD", false) })
        {
            context.Assets.Add(new Asset
            {
                Slug = slug,
                Symbol = symbol,
                Name = slug,
                CoinGeckoId = slug,
                IsActive = active,
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now,
                Price = new AssetPrice { PriceUsd = 100m, Source = DataSource.CoinGecko, UpdatedAtUtc = Now.AddMinutes(-5) }
            });
        }

        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private WatchlistService Service(AppDbContext? context = null)
    {
        context ??= _db.CreateContext();
        var assets = new AssetQueryService(context, _cache, _time, Options.Create(new LivePriceOptions { StaleSeconds = 60 }));
        return new WatchlistService(context, assets, _time);
    }

    [Fact]
    public async Task Add_then_list_returns_the_asset()
    {
        Assert.True((await Service().AddAsync(Alice, "bitcoin")).Succeeded);

        var list = await Service().ListAsync(Alice);

        var item = Assert.Single(list);
        Assert.Equal("bitcoin", item.Asset.Slug);
        Assert.Equal(Now, item.AddedAtUtc);
    }

    [Fact]
    public async Task Add_is_idempotent_and_normalizes_the_slug()
    {
        await Service().AddAsync(Alice, "bitcoin");
        var again = await Service().AddAsync(Alice, "  BitCoin ");

        Assert.True(again.Succeeded);
        Assert.Single(await Service().ListAsync(Alice));
    }

    [Fact]
    public async Task Add_unknown_or_inactive_asset_is_not_found()
    {
        foreach (var slug in new[] { "no-existe", "muerta", "", "   " })
        {
            var result = await Service().AddAsync(Alice, slug);
            Assert.False(result.Succeeded);
            Assert.Equal(WatchlistFailure.NotFound, result.Failure);
        }

        Assert.Empty(await Service().ListAsync(Alice));
    }

    [Fact]
    public async Task List_is_newest_first_and_private_per_user()
    {
        await Service().AddAsync(Alice, "bitcoin");
        _time.Advance(TimeSpan.FromMinutes(1));
        await Service().AddAsync(Alice, "ethereum");
        await Service().AddAsync(Bob, "bitcoin");

        Assert.Equal(new[] { "ethereum", "bitcoin" }, (await Service().ListAsync(Alice)).Select(i => i.Asset.Slug));
        Assert.Equal(new[] { "bitcoin" }, (await Service().ListAsync(Bob)).Select(i => i.Asset.Slug));
    }

    [Fact]
    public async Task Remove_is_idempotent_and_only_affects_that_user()
    {
        await Service().AddAsync(Alice, "bitcoin");
        await Service().AddAsync(Bob, "bitcoin");

        await Service().RemoveAsync(Alice, "bitcoin");
        await Service().RemoveAsync(Alice, "bitcoin");
        await Service().RemoveAsync(Alice, "no-existe");

        Assert.Empty(await Service().ListAsync(Alice));
        Assert.Single(await Service().ListAsync(Bob));
    }

    [Fact]
    public async Task Inactive_assets_are_hidden_from_the_list_but_can_still_be_removed()
    {
        await Service().AddAsync(Alice, "bitcoin");
        await Service().AddAsync(Alice, "ethereum");

        using (var context = _db.CreateContext())
        {
            var eth = await context.Assets.SingleAsync(a => a.Slug == "ethereum");
            eth.IsActive = false;
            await context.SaveChangesAsync();
        }

        Assert.Equal(new[] { "bitcoin" }, (await Service().ListAsync(Alice)).Select(i => i.Asset.Slug));

        await Service().RemoveAsync(Alice, "ethereum");
        using var check = _db.CreateContext();
        Assert.Equal(1, await check.WatchlistItems.CountAsync(w => w.UserId == Alice));
    }

    [Fact]
    public async Task Add_stops_at_the_limit()
    {
        using (var context = _db.CreateContext())
        {
            for (var i = 0; i < IWatchlistService.MaxItems; i++)
            {
                context.Assets.Add(new Asset
                {
                    Slug = $"moneda-{i}",
                    Symbol = $"M{i}",
                    Name = $"Moneda {i}",
                    CoinGeckoId = $"moneda-{i}",
                    IsActive = true,
                    CreatedAtUtc = Now,
                    UpdatedAtUtc = Now
                });
            }

            context.SaveChanges();
        }

        for (var i = 0; i < IWatchlistService.MaxItems; i++)
        {
            Assert.True((await Service().AddAsync(Alice, $"moneda-{i}")).Succeeded);
        }

        var over = await Service().AddAsync(Alice, "bitcoin");
        Assert.False(over.Succeeded);
        Assert.Equal(WatchlistFailure.LimitReached, over.Failure);

        // Seguir algo que ya se sigue sigue siendo válido aunque se esté en el límite.
        Assert.True((await Service().AddAsync(Alice, "moneda-0")).Succeeded);
        // Y el límite es por usuario.
        Assert.True((await Service().AddAsync(Bob, "bitcoin")).Succeeded);
    }
}
