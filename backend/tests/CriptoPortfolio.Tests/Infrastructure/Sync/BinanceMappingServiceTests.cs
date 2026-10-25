using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Prices;
using CriptoPortfolio.Domain;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Infrastructure.Prices;
using CriptoPortfolio.Infrastructure.Sync;
using CriptoPortfolio.Tests.Infrastructure.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Sync;

public class BinanceMappingServiceTests : IDisposable
{
    private sealed class FakeClient : IBinanceMarketClient
    {
        public Func<IReadOnlyDictionary<string, decimal>> Prices { get; set; } =
            () => new Dictionary<string, decimal>();

        public Task<IReadOnlyDictionary<string, decimal>> GetUsdtPricesAsync(CancellationToken ct = default) =>
            Task.FromResult(Prices());
    }

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeClient _client = new();

    public void Dispose() => _db.Dispose();

    private static Dictionary<string, decimal> Pairs(params (string Pair, decimal Price)[] items) =>
        items.ToDictionary(i => i.Pair, i => i.Price);

    private async Task<int> AddAssetAsync(string symbol, int? rank, decimal? geckoPrice = null,
        bool active = true, string? binance = null)
    {
        await using var context = _db.CreateContext();
        var now = _time.GetUtcNow().UtcDateTime;
        var asset = new Asset
        {
            Symbol = symbol,
            Name = symbol,
            Slug = $"{symbol.ToLowerInvariant()}-{rank}",
            CoinGeckoId = $"{symbol.ToLowerInvariant()}-{rank}",
            Rank = rank,
            IsActive = active,
            BinanceSymbol = binance,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        if (geckoPrice is not null)
        {
            asset.Price = new AssetPrice { PriceUsd = geckoPrice.Value, Source = DataSource.CoinGecko, UpdatedAtUtc = now };
        }

        context.Assets.Add(asset);
        await context.SaveChangesAsync();
        return asset.Id;
    }

    private Task<BinanceMappingResult> RunAsync(string owner = "A", decimal maxDeviation = 0.25m)
    {
        var context = _db.CreateContext();
        var locks = new SyncLockService(context, _time, NullLogger<SyncLockService>.Instance);
        var service = new BinanceMappingService(
            context, _client, locks, _time,
            Options.Create(new LivePriceOptions { MaxPriceDeviation = maxDeviation }),
            NullLogger<BinanceMappingService>.Instance);
        return service.SyncAsync(owner);
    }

    private async Task<Asset> LoadAsync(int id)
    {
        await using var context = _db.CreateContext();
        return await context.Assets.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    [Fact]
    public async Task Assigns_the_pair_and_sets_binance_as_history_source()
    {
        var id = await AddAssetAsync("BTC", 1, 80000m);
        _client.Prices = () => Pairs(("BTCUSDT", 81000m));

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Assigned);
        var asset = await LoadAsync(id);
        Assert.Equal("BTCUSDT", asset.BinanceSymbol);
        Assert.Equal(DataSource.Binance, asset.HistorySource);
    }

    [Fact]
    public async Task Best_rank_wins_when_symbols_are_shared()
    {
        var worse = await AddAssetAsync("ABC", 400, 1m);
        var better = await AddAssetAsync("ABC", 50, 1m);
        _client.Prices = () => Pairs(("ABCUSDT", 1m));

        await RunAsync();

        Assert.Equal("ABCUSDT", (await LoadAsync(better)).BinanceSymbol);
        Assert.Null((await LoadAsync(worse)).BinanceSymbol);
    }

    [Fact]
    public async Task Skips_symbols_without_a_pair()
    {
        var id = await AddAssetAsync("XYZ", 10, 1m);
        _client.Prices = () => Pairs(("BTCUSDT", 80000m));

        var result = await RunAsync();

        Assert.Equal(0, result.Assigned);
        Assert.Null((await LoadAsync(id)).BinanceSymbol);
    }

    [Fact]
    public async Task Rejects_a_pair_whose_price_is_far_from_coingecko()
    {
        var id = await AddAssetAsync("FAKE", 10, 1m);
        _client.Prices = () => Pairs(("FAKEUSDT", 1000m));

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Assigned);
        Assert.Equal(1, result.Rejected);
        Assert.Null((await LoadAsync(id)).BinanceSymbol);
    }

    [Fact]
    public async Task Accepts_when_there_is_no_coingecko_price_to_compare()
    {
        var id = await AddAssetAsync("NEW", 10);
        _client.Prices = () => Pairs(("NEWUSDT", 3m));

        var result = await RunAsync();

        Assert.Equal(1, result.Assigned);
        Assert.Equal("NEWUSDT", (await LoadAsync(id)).BinanceSymbol);
    }

    [Fact]
    public async Task Releases_a_pair_that_disappeared()
    {
        var id = await AddAssetAsync("OLD", 10, 1m, binance: "OLDUSDT");
        _client.Prices = () => Pairs(("BTCUSDT", 80000m));

        var result = await RunAsync();

        Assert.Equal(1, result.Released);
        Assert.Null((await LoadAsync(id)).BinanceSymbol);
    }

    [Fact]
    public async Task Moves_a_pair_from_the_worse_to_the_better_asset()
    {
        var worse = await AddAssetAsync("ABC", 400, 1m, binance: "ABCUSDT");
        var better = await AddAssetAsync("ABC", 50, 1m);
        _client.Prices = () => Pairs(("ABCUSDT", 1m));

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Assigned);
        Assert.Equal(1, result.Released);
        Assert.Null((await LoadAsync(worse)).BinanceSymbol);
        Assert.Equal("ABCUSDT", (await LoadAsync(better)).BinanceSymbol);
    }

    [Fact]
    public async Task Inactive_assets_lose_their_pair()
    {
        var id = await AddAssetAsync("DEAD", 10, 1m, active: false, binance: "DEADUSDT");
        _client.Prices = () => Pairs(("DEADUSDT", 1m));

        var result = await RunAsync();

        Assert.Equal(1, result.Released);
        Assert.Null((await LoadAsync(id)).BinanceSymbol);
    }

    [Fact]
    public async Task Binance_failure_is_recorded_and_leaves_assets_untouched()
    {
        var id = await AddAssetAsync("BTC", 1, 80000m, binance: "BTCUSDT");
        _client.Prices = () => throw new HttpRequestException("boom");

        var result = await RunAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Equal("BTCUSDT", (await LoadAsync(id)).BinanceSymbol);

        await using var context = _db.CreateContext();
        var state = await context.SyncStates.AsNoTracking().SingleAsync(s => s.Name == SyncJobNames.BinanceMapping);
        Assert.NotNull(state.LastError);

        // El candado quedó libre: otra instancia puede ejecutar enseguida.
        _client.Prices = () => Pairs(("BTCUSDT", 80000m));
        Assert.True((await RunAsync("B")).Executed);
    }

    [Fact]
    public async Task Empty_pair_list_is_an_error_and_changes_nothing()
    {
        var id = await AddAssetAsync("BTC", 1, 80000m, binance: "BTCUSDT");
        _client.Prices = () => new Dictionary<string, decimal>();

        var result = await RunAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("BTCUSDT", (await LoadAsync(id)).BinanceSymbol);
    }

    [Fact]
    public async Task Is_skipped_when_another_owner_holds_the_lock()
    {
        await AddAssetAsync("BTC", 1, 80000m);
        _client.Prices = () => Pairs(("BTCUSDT", 80000m));

        await using (var context = _db.CreateContext())
        {
            var locks = new SyncLockService(context, _time, NullLogger<SyncLockService>.Instance);
            Assert.True(await locks.TryAcquireAsync(SyncJobNames.BinanceMapping, "OTHER", TimeSpan.FromMinutes(5)));
        }

        var result = await RunAsync("A");

        Assert.False(result.Executed);
        Assert.Equal(0, result.Assigned);
    }

    [Fact]
    public async Task Second_run_changes_nothing()
    {
        await AddAssetAsync("BTC", 1, 80000m);
        _client.Prices = () => Pairs(("BTCUSDT", 80000m));

        await RunAsync();
        var second = await RunAsync();

        Assert.True(second.Succeeded);
        Assert.Equal(0, second.Assigned);
        Assert.Equal(0, second.Released);
        Assert.Equal(0, second.Rejected);
    }
}
