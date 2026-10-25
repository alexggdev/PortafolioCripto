using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Catalog;
using CriptoPortfolio.Domain;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Infrastructure.Sync;
using CriptoPortfolio.Tests.Infrastructure.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Sync;

public class CatalogSyncServiceTests : IDisposable
{
    private sealed class FakeProvider : ICatalogProvider
    {
        public Func<IReadOnlyList<CatalogCoin>> Coins { get; set; } = () => Array.Empty<CatalogCoin>();

        public int Calls { get; private set; }

        public Task<IReadOnlyList<CatalogCoin>> GetTopCoinsAsync(int count, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Coins());
        }
    }

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeProvider _provider = new();

    public void Dispose() => _db.Dispose();

    private static CatalogCoin Coin(string id, int rank, decimal? price = 1m, string? name = null) =>
        new(id, id.ToUpperInvariant(), name ?? id, null, rank, price, 1000m, 10m, 0.1m, 1.5m, 7m);

    private static List<CatalogCoin> Coins(int count) =>
        Enumerable.Range(1, count).Select(i => Coin($"coin-{i}", i, i)).ToList();

    // Un contexto nuevo por pasada, como un scope por ejecución del worker.
    private Task<CatalogSyncResult> RunAsync(string owner = "A", int topCount = 10)
    {
        var context = _db.CreateContext();
        var locks = new SyncLockService(context, _time, NullLogger<SyncLockService>.Instance);
        var service = new CatalogSyncService(
            context, _provider, locks, _time,
            Options.Create(new CatalogSyncOptions { TopCount = topCount }),
            NullLogger<CatalogSyncService>.Instance);
        return service.SyncAsync(owner);
    }

    private async Task<List<Asset>> LoadAssetsAsync()
    {
        await using var context = _db.CreateContext();
        return await context.Assets.AsNoTracking().Include(a => a.Price).OrderBy(a => a.Rank).ToListAsync();
    }

    private async Task<SyncState> LoadStateAsync()
    {
        await using var context = _db.CreateContext();
        return await context.SyncStates.AsNoTracking().SingleAsync(s => s.Name == SyncJobNames.Catalog);
    }

    [Fact]
    public async Task First_sync_creates_assets_with_price_rank_and_slug()
    {
        _provider.Coins = () => Coins(3);

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Processed);
        Assert.Equal(3, result.Created);

        var assets = await LoadAssetsAsync();
        Assert.Equal(3, assets.Count);
        var first = assets[0];
        Assert.Equal("coin-1", first.Slug);
        Assert.Equal("coin-1", first.CoinGeckoId);
        Assert.Equal("COIN-1", first.Symbol);
        Assert.Equal(1, first.Rank);
        Assert.True(first.IsActive);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, first.CreatedAtUtc);
        Assert.NotNull(first.Price);
        Assert.Equal(1m, first.Price!.PriceUsd);
        Assert.Equal(DataSource.CoinGecko, first.Price.Source);
        Assert.Equal(1.5m, first.Price.Change24hPct);
    }

    [Fact]
    public async Task Sync_records_success_and_frees_the_lock()
    {
        _provider.Coins = () => Coins(3);

        await RunAsync();

        var state = await LoadStateAsync();
        Assert.Null(state.LeaseOwner);
        Assert.Null(state.LastError);
        Assert.Equal(3, state.LastItemsProcessed);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, state.LastSucceededAtUtc);
    }

    [Fact]
    public async Task Second_sync_updates_in_place_without_duplicates()
    {
        _provider.Coins = () => Coins(3);
        await RunAsync();

        _time.Advance(TimeSpan.FromMinutes(15));
        _provider.Coins = () => new List<CatalogCoin>
        {
            Coin("coin-1", 2, 50m, "Renombrada"),
            Coin("coin-2", 1, 60m),
            Coin("coin-3", 3, 70m)
        };
        var result = await RunAsync();

        Assert.Equal(0, result.Created);
        var assets = await LoadAssetsAsync();
        Assert.Equal(3, assets.Count);

        var renamed = assets.Single(a => a.CoinGeckoId == "coin-1");
        Assert.Equal("Renombrada", renamed.Name);
        Assert.Equal(2, renamed.Rank);
        Assert.Equal(50m, renamed.Price!.PriceUsd);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, renamed.UpdatedAtUtc);
        Assert.Equal(_time.GetUtcNow().UtcDateTime.AddMinutes(-15), renamed.CreatedAtUtc); // la creación no se pisa
    }

    [Fact]
    public async Task Coin_that_leaves_the_top_is_deactivated_never_deleted()
    {
        _provider.Coins = () => Coins(10);
        await RunAsync();

        _provider.Coins = () => Coins(9); // coin-10 sale: 9 de 10 = 90 % de cobertura
        var result = await RunAsync();

        Assert.Equal(1, result.Deactivated);
        var assets = await LoadAssetsAsync();
        Assert.Equal(10, assets.Count);
        Assert.False(assets.Single(a => a.CoinGeckoId == "coin-10").IsActive);
        Assert.Equal(9, assets.Count(a => a.IsActive));
    }

    [Fact]
    public async Task Nothing_is_deactivated_when_the_provider_returns_too_few_coins()
    {
        _provider.Coins = () => Coins(10);
        await RunAsync();

        _provider.Coins = () => Coins(3); // 30 %: parece un fallo del proveedor, no un cambio del ranking
        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Deactivated);
        Assert.All(await LoadAssetsAsync(), a => Assert.True(a.IsActive));
    }

    [Fact]
    public async Task A_coin_that_returns_to_the_top_is_reactivated()
    {
        _provider.Coins = () => Coins(10);
        await RunAsync();
        _provider.Coins = () => Coins(9);
        await RunAsync();

        _provider.Coins = () => Coins(10);
        await RunAsync();

        Assert.All(await LoadAssetsAsync(), a => Assert.True(a.IsActive));
    }

    [Fact]
    public async Task Coin_without_price_is_created_without_a_price_row()
    {
        _provider.Coins = () => new List<CatalogCoin> { Coin("sin-precio", 1, price: null) };

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        var asset = Assert.Single(await LoadAssetsAsync());
        Assert.Null(asset.Price);
    }

    [Fact]
    public async Task Out_of_range_values_are_dropped_instead_of_failing_the_whole_batch()
    {
        // Cifras que no caben en decimal(38,18) / decimal(18,6): en SQL Server darían "Conversion overflows".
        var broken = new CatalogCoin("roto", "ROTO", "Roto", null, 2,
            PriceUsd: 5m,
            MarketCapUsd: 1e25m,
            Volume24hUsd: 10m,
            Change1hPct: 0.1m,
            Change24hPct: 1e15m,
            Change7dPct: -1e15m);
        _provider.Coins = () => new List<CatalogCoin> { Coin("bueno", 1, 1m), broken };

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Failed);   // la moneda rota se cuenta como incidencia
        Assert.Equal(1, result.Processed);

        var assets = await LoadAssetsAsync();
        Assert.Equal(2, assets.Count);

        var roto = assets.Single(a => a.CoinGeckoId == "roto");
        Assert.Equal(5m, roto.Price!.PriceUsd);          // lo válido se conserva
        Assert.Equal(10m, roto.Price.Volume24hUsd);
        Assert.Null(roto.Price.MarketCapUsd);            // lo imposible se descarta
        Assert.Null(roto.Price.Change24hPct);
        Assert.Null(roto.Price.Change7dPct);
    }

    [Fact]
    public async Task A_negative_or_absurd_price_leaves_the_coin_without_a_price_row()
    {
        _provider.Coins = () => new List<CatalogCoin>
        {
            Coin("negativa", 1, price: -1m),
            Coin("enorme", 2, price: 1e25m)
        };

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Failed);
        Assert.All(await LoadAssetsAsync(), a => Assert.Null(a.Price));
    }

    [Fact]
    public async Task Provider_failure_is_recorded_not_thrown_and_the_lock_is_released()
    {
        _provider.Coins = () => throw new HttpRequestException("429 Too Many Requests");

        var result = await RunAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("429", result.Error);
        Assert.Empty(await LoadAssetsAsync());

        var state = await LoadStateAsync();
        Assert.Null(state.LeaseOwner);
        Assert.Contains("429", state.LastError);
        Assert.Null(state.LastSucceededAtUtc);
    }

    [Fact]
    public async Task Failure_keeps_the_existing_catalog_untouched()
    {
        _provider.Coins = () => Coins(10);
        await RunAsync();

        _provider.Coins = () => throw new HttpRequestException("caído");
        await RunAsync();

        var assets = await LoadAssetsAsync();
        Assert.Equal(10, assets.Count);
        Assert.All(assets, a => Assert.True(a.IsActive));
    }

    [Fact]
    public async Task Empty_provider_response_is_an_error_and_deactivates_nothing()
    {
        _provider.Coins = () => Coins(10);
        await RunAsync();

        _provider.Coins = () => Array.Empty<CatalogCoin>();
        var result = await RunAsync();

        Assert.False(result.Succeeded);
        Assert.All(await LoadAssetsAsync(), a => Assert.True(a.IsActive));
    }

    [Fact]
    public async Task Sync_is_skipped_while_another_instance_holds_the_lock()
    {
        _provider.Coins = () => Coins(3);
        await using (var context = _db.CreateContext())
        {
            var locks = new SyncLockService(context, _time, NullLogger<SyncLockService>.Instance);
            Assert.True(await locks.TryAcquireAsync(SyncJobNames.Catalog, "OTRA", TimeSpan.FromMinutes(10)));
        }

        var result = await RunAsync("A");

        Assert.False(result.Executed);
        Assert.Equal(0, _provider.Calls); // ni siquiera se llamó al proveedor
        Assert.Empty(await LoadAssetsAsync());
    }
}
