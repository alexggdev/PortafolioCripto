using System.Net;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.History;
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

public class HistorySyncServiceTests : IDisposable
{
    private sealed class FakeBinance : IBinanceHistoryClient
    {
        public Func<string, DateOnly, IReadOnlyList<DailyPrice>> Handler { get; set; } = (_, _) => Array.Empty<DailyPrice>();

        public List<(string Symbol, DateOnly From)> Calls { get; } = new();

        public Task<IReadOnlyList<DailyPrice>> GetDailyPricesAsync(string symbol, DateOnly from, CancellationToken ct = default)
        {
            Calls.Add((symbol, from));
            return Task.FromResult(Handler(symbol, from));
        }
    }

    private sealed class FakeGecko : ICoinGeckoHistoryClient
    {
        public Func<string, int, IReadOnlyList<DailyPrice>> Handler { get; set; } = (_, _) => Array.Empty<DailyPrice>();

        public List<(string Id, int Days)> Calls { get; } = new();

        public Task<IReadOnlyList<DailyPrice>> GetDailyPricesAsync(string coinGeckoId, int days, CancellationToken ct = default)
        {
            Calls.Add((coinGeckoId, days));
            return Task.FromResult(Handler(coinGeckoId, days));
        }
    }

    private static readonly DateOnly Today = new(2026, 10, 13);
    private static readonly DateOnly Yesterday = new(2026, 10, 12);

    private readonly SqliteTestDb _db = new();
    // 13 oct 2026, 10:00 UTC (la ventana de snapshot, de 00:00 a 00:30, ya pasó).
    private FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero));
    private readonly FakeBinance _binance = new();
    private readonly FakeGecko _gecko = new();
    private readonly HistoryRetryGate _gate = new();

    public void Dispose() => _db.Dispose();

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private static DailyPrice Day(DateOnly date, decimal close = 10m) =>
        new(date, close, close - 1, close + 1, close - 2, 1000m);

    private static IReadOnlyList<DailyPrice> Days(DateOnly from, DateOnly to, decimal close = 10m)
    {
        var list = new List<DailyPrice>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            list.Add(Day(d, close));
        }

        return list;
    }

    private async Task<int> AddAssetAsync(
        string slug, int rank, string? binance = null, string? gecko = null,
        DataSource historySource = DataSource.CoinGecko, bool active = true,
        decimal? price = null, DateTime? priceUpdatedAt = null)
    {
        await using var context = _db.CreateContext();
        var asset = new Asset
        {
            Slug = slug,
            Symbol = slug.ToUpperInvariant(),
            Name = slug,
            Rank = rank,
            IsActive = active,
            BinanceSymbol = binance,
            CoinGeckoId = gecko,
            HistorySource = historySource,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        if (price is not null)
        {
            asset.Price = new AssetPrice
            {
                PriceUsd = price.Value,
                Volume24hUsd = 777m,
                Source = DataSource.CoinGecko,
                UpdatedAtUtc = priceUpdatedAt ?? Now
            };
        }

        context.Assets.Add(asset);
        await context.SaveChangesAsync();
        return asset.Id;
    }

    private async Task AddRowsAsync(int assetId, DataSource source, DateOnly from, DateOnly to)
    {
        await using var context = _db.CreateContext();
        foreach (var d in Days(from, to))
        {
            context.AssetDailyPrices.Add(new AssetDailyPrice
            {
                AssetId = assetId, DateUtc = d.DateUtc, CloseUsd = d.CloseUsd, Source = source
            });
        }

        await context.SaveChangesAsync();
    }

    private Task<HistorySyncResult> RunAsync(string owner = "A")
    {
        var context = _db.CreateContext();
        var locks = new SyncLockService(context, _time, NullLogger<SyncLockService>.Instance);
        var service = new HistorySyncService(
            context, _binance, _gecko, locks, _gate, _time,
            Options.Create(new HistorySyncOptions
            {
                BinanceInitialDays = 5,
                CoinGeckoInitialDays = 7,
                BinanceDelayMs = 0,
                CoinGeckoDelayMs = 0,
                SnapshotWindowMinutes = 30,
                MaxConsecutiveFailures = 3
            }),
            NullLogger<HistorySyncService>.Instance);
        return service.SyncAsync(owner);
    }

    private async Task<List<AssetDailyPrice>> RowsAsync(int assetId)
    {
        await using var context = _db.CreateContext();
        return await context.AssetDailyPrices.AsNoTracking()
            .Where(d => d.AssetId == assetId).OrderBy(d => d.DateUtc).ToListAsync();
    }

    private async Task<Asset> LoadAsync(int id)
    {
        await using var context = _db.CreateContext();
        return await context.Assets.AsNoTracking().SingleAsync(a => a.Id == id);
    }

    [Fact]
    public async Task Binance_initial_load_asks_from_the_initial_days_and_stores_ohlc()
    {
        var id = await AddAssetAsync("btc", 1, binance: "BTCUSDT", historySource: DataSource.Binance);
        _binance.Handler = (_, from) => Days(from, Yesterday);

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Processed);
        Assert.Equal(5, result.Inserted);
        Assert.Equal(("BTCUSDT", Today.AddDays(-5)), _binance.Calls.Single());

        var rows = await RowsAsync(id);
        Assert.Equal(5, rows.Count);
        Assert.Equal(Yesterday, rows[^1].DateUtc);
        Assert.All(rows, r => Assert.Equal(DataSource.Binance, r.Source));
        Assert.NotNull(rows[0].OpenUsd);
        Assert.NotNull(rows[0].HighUsd);
        Assert.NotNull(rows[0].LowUsd);
        Assert.Equal(1000m, rows[0].VolumeUsd);
    }

    [Fact]
    public async Task Binance_incremental_asks_from_the_day_after_the_last_one()
    {
        var id = await AddAssetAsync("btc", 1, binance: "BTCUSDT", historySource: DataSource.Binance);
        await AddRowsAsync(id, DataSource.Binance, new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 10));
        _binance.Handler = (_, from) => Days(from, Yesterday);

        var result = await RunAsync();

        Assert.Equal(2, result.Inserted);
        Assert.Equal(new DateOnly(2026, 10, 11), _binance.Calls.Single().From);
        Assert.Equal(Yesterday, (await RowsAsync(id))[^1].DateUtc);
    }

    [Fact]
    public async Task Up_to_date_assets_make_no_external_calls()
    {
        var btc = await AddAssetAsync("btc", 1, binance: "BTCUSDT", historySource: DataSource.Binance);
        var eth = await AddAssetAsync("eth", 2, gecko: "ethereum");
        await AddRowsAsync(btc, DataSource.Binance, new DateOnly(2026, 10, 10), Yesterday);
        await AddRowsAsync(eth, DataSource.CoinGecko, new DateOnly(2026, 10, 10), Yesterday);

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Processed);
        Assert.Empty(_binance.Calls);
        Assert.Empty(_gecko.Calls);
    }

    [Fact]
    public async Task Ignores_rows_of_today_and_rows_already_stored()
    {
        var id = await AddAssetAsync("btc", 1, binance: "BTCUSDT", historySource: DataSource.Binance);
        await AddRowsAsync(id, DataSource.Binance, new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 10));
        _binance.Handler = (_, _) => Days(new DateOnly(2026, 10, 9), Today); // repite 9 y 10 y añade hoy

        var result = await RunAsync();

        Assert.Equal(2, result.Inserted); // solo el 11 y el 12
        var rows = await RowsAsync(id);
        Assert.Equal(4, rows.Count);
        Assert.DoesNotContain(rows, r => r.DateUtc == Today);
    }

    [Fact]
    public async Task Coingecko_initial_load_uses_the_initial_days_and_stores_only_close()
    {
        var id = await AddAssetAsync("eth", 2, gecko: "ethereum");
        _gecko.Handler = (_, days) => Days(Today.AddDays(-days), Yesterday).Select(d => new DailyPrice(d.DateUtc, d.CloseUsd)).ToList();

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(("ethereum", 7), _gecko.Calls.Single());
        var rows = await RowsAsync(id);
        Assert.Equal(7, rows.Count);
        Assert.All(rows, r =>
        {
            Assert.Equal(DataSource.CoinGecko, r.Source);
            Assert.Null(r.OpenUsd);
        });
    }

    [Fact]
    public async Task Coingecko_missing_yesterday_is_taken_from_the_catalog_price_without_api_call()
    {
        var id = await AddAssetAsync("eth", 2, gecko: "ethereum", price: 1850m,
            priceUpdatedAt: new DateTime(2026, 10, 13, 0, 10, 0, DateTimeKind.Utc));
        await AddRowsAsync(id, DataSource.CoinGecko, new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 11));

        var result = await RunAsync();

        Assert.Empty(_gecko.Calls);
        Assert.Equal(1, result.Inserted);
        var last = (await RowsAsync(id))[^1];
        Assert.Equal(Yesterday, last.DateUtc);
        Assert.Equal(1850m, last.CloseUsd);
        Assert.Equal(777m, last.VolumeUsd);
        Assert.Equal(DataSource.CoinGecko, last.Source);
    }

    [Fact]
    public async Task Coingecko_waits_when_the_catalog_price_of_the_new_day_is_not_there_yet()
    {
        var id = await AddAssetAsync("eth", 2, gecko: "ethereum", price: 1850m,
            priceUpdatedAt: new DateTime(2026, 10, 12, 23, 50, 0, DateTimeKind.Utc));
        await AddRowsAsync(id, DataSource.CoinGecko, new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 11));
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 0, 10, 0, TimeSpan.Zero)); // el reloj no puede ir hacia atrás

        var result = await RunAsync();

        Assert.Empty(_gecko.Calls);
        Assert.Equal(1, result.Pending);
        Assert.Equal(0, result.Inserted);
    }

    [Fact]
    public async Task Coingecko_falls_back_to_the_api_when_the_snapshot_window_has_passed()
    {
        var id = await AddAssetAsync("eth", 2, gecko: "ethereum", price: 1850m,
            priceUpdatedAt: new DateTime(2026, 10, 13, 9, 50, 0, DateTimeKind.Utc)); // fuera de la ventana
        await AddRowsAsync(id, DataSource.CoinGecko, new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 11));
        _gecko.Handler = (_, _) => new[] { Day(new DateOnly(2026, 10, 11), 5m), Day(Yesterday, 1840m) };

        var result = await RunAsync();

        Assert.Equal(("ethereum", 3), _gecko.Calls.Single()); // hueco 1 + 2
        Assert.Equal(1, result.Inserted);
        Assert.Equal(1840m, (await RowsAsync(id))[^1].CloseUsd);
    }

    [Fact]
    public async Task Coingecko_bigger_gap_asks_the_api_with_gap_plus_two_days_capped()
    {
        var id = await AddAssetAsync("eth", 2, gecko: "ethereum");
        await AddRowsAsync(id, DataSource.CoinGecko, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 8)); // faltan 9..12 = 4 días
        _gecko.Handler = (_, _) => Days(new DateOnly(2026, 10, 9), Yesterday);

        var result = await RunAsync();

        Assert.Equal(("ethereum", 6), _gecko.Calls.Single());
        Assert.Equal(4, result.Inserted);
    }

    [Fact]
    public async Task Changing_source_replaces_the_old_rows_with_the_new_source()
    {
        // Antes en CoinGecko; ahora tiene par de Binance.
        var id = await AddAssetAsync("btc", 1, binance: "BTCUSDT", gecko: "bitcoin", historySource: DataSource.Binance);
        await AddRowsAsync(id, DataSource.CoinGecko, new DateOnly(2026, 10, 1), Yesterday);
        _binance.Handler = (_, from) => Days(from, Yesterday, 99m);

        var result = await RunAsync();

        Assert.True(result.Succeeded);
        var rows = await RowsAsync(id);
        Assert.Equal(5, rows.Count); // carga inicial de Binance
        Assert.All(rows, r =>
        {
            Assert.Equal(DataSource.Binance, r.Source);
            Assert.Equal(99m, r.CloseUsd);
        });
        Assert.Equal(DataSource.Binance, (await LoadAsync(id)).HistorySource);
    }

    [Fact]
    public async Task Losing_the_binance_pair_reloads_from_coingecko_and_updates_the_history_source()
    {
        var id = await AddAssetAsync("old", 5, gecko: "old-coin", historySource: DataSource.Binance);
        await AddRowsAsync(id, DataSource.Binance, new DateOnly(2026, 10, 1), Yesterday);
        _gecko.Handler = (_, days) => Days(Today.AddDays(-days), Yesterday, 7m);

        await RunAsync();

        var rows = await RowsAsync(id);
        Assert.Equal(7, rows.Count);
        Assert.All(rows, r => Assert.Equal(DataSource.CoinGecko, r.Source));
        Assert.Equal(DataSource.CoinGecko, (await LoadAsync(id)).HistorySource);
    }

    [Fact]
    public async Task Up_to_date_asset_with_the_wrong_history_source_label_only_fixes_the_label()
    {
        var id = await AddAssetAsync("old", 5, gecko: "old-coin", historySource: DataSource.Binance);
        await AddRowsAsync(id, DataSource.CoinGecko, new DateOnly(2026, 10, 1), Yesterday);

        await RunAsync();

        Assert.Empty(_gecko.Calls);
        Assert.Equal(DataSource.CoinGecko, (await LoadAsync(id)).HistorySource);
        Assert.Equal(12, (await RowsAsync(id)).Count);
    }

    [Fact]
    public async Task Empty_answer_keeps_the_old_rows_and_is_not_asked_again_right_away()
    {
        var id = await AddAssetAsync("new", 1, binance: "NEWUSDT", gecko: "new-coin", historySource: DataSource.Binance);
        await AddRowsAsync(id, DataSource.CoinGecko, new DateOnly(2026, 10, 8), Yesterday);
        _binance.Handler = (_, _) => Array.Empty<DailyPrice>();

        await RunAsync();
        await RunAsync();

        Assert.Single(_binance.Calls); // la segunda pasada no insiste
        var rows = await RowsAsync(id);
        Assert.Equal(5, rows.Count);
        Assert.All(rows, r => Assert.Equal(DataSource.CoinGecko, r.Source));
    }

    [Fact]
    public async Task Inactive_assets_and_assets_without_any_id_are_skipped()
    {
        await AddAssetAsync("off", 1, binance: "OFFUSDT", active: false);
        await AddAssetAsync("none", 2);

        var result = await RunAsync();

        Assert.Equal(0, result.Processed);
        Assert.Empty(_binance.Calls);
        Assert.Empty(_gecko.Calls);
    }

    [Fact]
    public async Task One_failing_asset_does_not_stop_the_others()
    {
        var bad = await AddAssetAsync("bad", 1, binance: "BADUSDT", historySource: DataSource.Binance);
        var good = await AddAssetAsync("good", 2, binance: "GOODUSDT", historySource: DataSource.Binance);
        _binance.Handler = (symbol, from) =>
            symbol == "BADUSDT" ? throw new HttpRequestException("boom") : Days(from, Yesterday);

        var result = await RunAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.Processed);
        Assert.Empty(await RowsAsync(bad));
        Assert.Equal(5, (await RowsAsync(good)).Count);

        await using var context = _db.CreateContext();
        var state = await context.SyncStates.AsNoTracking().SingleAsync(s => s.Name == SyncJobNames.DailyHistory);
        Assert.NotNull(state.LastError);
    }

    [Fact]
    public async Task Rate_limit_stops_that_provider_but_not_the_other()
    {
        await AddAssetAsync("a", 1, binance: "AUSDT", historySource: DataSource.Binance);
        await AddAssetAsync("b", 2, binance: "BUSDT", historySource: DataSource.Binance);
        var eth = await AddAssetAsync("eth", 3, gecko: "ethereum");
        _binance.Handler = (_, _) => throw new HttpRequestException("limit", null, HttpStatusCode.TooManyRequests);
        _gecko.Handler = (_, days) => Days(Today.AddDays(-days), Yesterday);

        var result = await RunAsync();

        Assert.Single(_binance.Calls); // tras el primer 429 no se llama más a Binance
        Assert.Equal(2, result.Pending); // las dos monedas de Binance quedan para otra pasada
        Assert.Equal(1, result.Processed);
        Assert.False(result.Succeeded);
        Assert.NotEmpty(await RowsAsync(eth));
    }

    [Fact]
    public async Task Is_skipped_when_another_owner_holds_the_lock()
    {
        await AddAssetAsync("btc", 1, binance: "BTCUSDT", historySource: DataSource.Binance);

        await using (var context = _db.CreateContext())
        {
            var locks = new SyncLockService(context, _time, NullLogger<SyncLockService>.Instance);
            Assert.True(await locks.TryAcquireAsync(SyncJobNames.DailyHistory, "OTHER", TimeSpan.FromMinutes(5)));
        }

        var result = await RunAsync("A");

        Assert.False(result.Executed);
        Assert.Empty(_binance.Calls);
    }

    [Fact]
    public async Task Second_run_changes_nothing()
    {
        await AddAssetAsync("btc", 1, binance: "BTCUSDT", historySource: DataSource.Binance);
        _binance.Handler = (_, from) => Days(from, Yesterday);

        await RunAsync();
        var second = await RunAsync();

        Assert.True(second.Succeeded);
        Assert.Equal(0, second.Processed);
        Assert.Equal(0, second.Inserted);
        Assert.Single(_binance.Calls);
    }
}
