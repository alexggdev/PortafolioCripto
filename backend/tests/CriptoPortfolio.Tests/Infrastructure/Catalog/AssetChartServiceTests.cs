using CriptoPortfolio.Application.Catalog;
using CriptoPortfolio.Application.Prices;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Infrastructure.Catalog;
using CriptoPortfolio.Infrastructure.Prices;
using CriptoPortfolio.Tests.Infrastructure.Support;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Catalog;

public class AssetChartServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 10, 0, 0, TimeSpan.Zero));
    private readonly LivePriceCache _cache = new();

    public void Dispose() => _db.Dispose();

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private AssetChartService Service()
    {
        var context = _db.CreateContext();
        var assets = new AssetQueryService(context, _cache, _time, Options.Create(new LivePriceOptions { StaleSeconds = 60 }));
        return new AssetChartService(context, assets, _time);
    }

    private async Task<int> AddAssetAsync(string slug, decimal? price = null, bool active = true)
    {
        await using var context = _db.CreateContext();
        var asset = new Asset
        {
            Slug = slug, Symbol = slug.ToUpperInvariant(), Name = slug, Rank = 1, IsActive = active,
            CreatedAtUtc = Now, UpdatedAtUtc = Now
        };
        if (price is not null)
        {
            asset.Price = new AssetPrice { PriceUsd = price.Value, Source = DataSource.CoinGecko, UpdatedAtUtc = Now.AddMinutes(-5) };
        }

        context.Assets.Add(asset);
        await context.SaveChangesAsync();
        return asset.Id;
    }

    // Una fila por día, desde 'daysAgo' hasta ayer; el cierre vale 100 + posición (sube 1 por día).
    private async Task AddHistoryAsync(int assetId, int daysAgo)
    {
        await using var context = _db.CreateContext();
        for (var i = daysAgo; i >= 1; i--)
        {
            context.AssetDailyPrices.Add(new AssetDailyPrice
            {
                AssetId = assetId,
                DateUtc = Today.AddDays(-i),
                CloseUsd = 100m + (daysAgo - i),
                Source = DataSource.Binance
            });
        }

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Unknown_or_inactive_slug_returns_null()
    {
        await AddAssetAsync("gone", 1m, active: false);

        Assert.Null(await Service().GetChartAsync("nope", ChartRange.Month));
        Assert.Null(await Service().GetChartAsync("gone", ChartRange.Month));
    }

    [Fact]
    public async Task Month_returns_the_last_30_days_in_order_plus_the_current_price()
    {
        var id = await AddAssetAsync("btc", price: 150m);
        await AddHistoryAsync(id, 100);

        var chart = await Service().GetChartAsync("btc", ChartRange.Month);

        Assert.NotNull(chart);
        Assert.Equal("1M", chart!.Range);
        Assert.Equal("btc", chart.Slug);
        Assert.True(chart.IncludesCurrentPrice);
        Assert.Equal(31, chart.Points.Count); // 30 cierres + hoy
        Assert.Equal(Today.AddDays(-30), chart.Points[0].Date);
        Assert.Equal(Today, chart.Points[^1].Date);
        Assert.Equal(150m, chart.Points[^1].PriceUsd);
        Assert.Equal(chart.Points.Select(p => p.Date).OrderBy(d => d), chart.Points.Select(p => p.Date));
    }

    [Fact]
    public async Task Week_excludes_older_rows()
    {
        var id = await AddAssetAsync("btc", price: 150m);
        await AddHistoryAsync(id, 100);

        var chart = await Service().GetChartAsync("btc", ChartRange.Week);

        Assert.Equal(8, chart!.Points.Count); // 7 cierres + hoy
        Assert.Equal(Today.AddDays(-7), chart.Points[0].Date);
    }

    [Fact]
    public async Task All_returns_every_row()
    {
        var id = await AddAssetAsync("btc", price: 500m);
        await AddHistoryAsync(id, 400);

        var chart = await Service().GetChartAsync("btc", ChartRange.All);

        Assert.Equal(401, chart!.Points.Count);
        Assert.Equal("All", chart.Range);
    }

    [Fact]
    public async Task Change_pct_compares_first_and_last_points()
    {
        var id = await AddAssetAsync("btc", price: 150m);
        await AddHistoryAsync(id, 10); // cierres 100..109

        var chart = await Service().GetChartAsync("btc", ChartRange.Week);

        // Primer punto: hace 7 días = 103; último: 150 → (150 - 103) / 103 = 45,63 %
        Assert.Equal(103m, chart!.Points[0].PriceUsd);
        Assert.Equal(45.63m, chart.ChangePct);
    }

    [Fact]
    public async Task Uses_the_live_price_for_the_current_point_when_it_is_fresh()
    {
        var id = await AddAssetAsync("btc", price: 150m);
        await AddHistoryAsync(id, 5);
        _cache.SetRange(new[] { new KeyValuePair<int, LivePrice>(id, new LivePrice(160m, Now.AddSeconds(-5))) });

        var chart = await Service().GetChartAsync("btc", ChartRange.Week);

        Assert.Equal(160m, chart!.Points[^1].PriceUsd);
    }

    [Fact]
    public async Task Without_any_current_price_the_series_has_only_closes()
    {
        var id = await AddAssetAsync("btc", price: null);
        await AddHistoryAsync(id, 5);

        var chart = await Service().GetChartAsync("btc", ChartRange.Week);

        Assert.False(chart!.IncludesCurrentPrice);
        Assert.Equal(5, chart.Points.Count);
        Assert.Equal(Today.AddDays(-1), chart.Points[^1].Date);
    }

    [Fact]
    public async Task Asset_without_history_returns_a_single_current_point_and_no_change()
    {
        await AddAssetAsync("new", price: 3m);

        var chart = await Service().GetChartAsync("new", ChartRange.Month);

        Assert.Single(chart!.Points);
        Assert.Null(chart.ChangePct);
    }

    [Fact]
    public async Task A_row_dated_today_is_replaced_by_the_current_price()
    {
        var id = await AddAssetAsync("btc", price: 150m);
        await using (var context = _db.CreateContext())
        {
            context.AssetDailyPrices.Add(new AssetDailyPrice
            {
                AssetId = id, DateUtc = Today, CloseUsd = 1m, Source = DataSource.Binance
            });
            await context.SaveChangesAsync();
        }

        var chart = await Service().GetChartAsync("btc", ChartRange.Week);

        var point = Assert.Single(chart!.Points);
        Assert.Equal(150m, point.PriceUsd);
    }

    [Fact]
    public async Task Long_series_are_downsampled_keeping_first_and_last()
    {
        var id = await AddAssetAsync("btc", price: 5000m);
        await AddHistoryAsync(id, 1500);

        var chart = await Service().GetChartAsync("btc", ChartRange.All);

        Assert.True(chart!.Points.Count <= AssetChartService.MaxPoints);
        Assert.True(chart.Points.Count > 400);
        Assert.Equal(Today.AddDays(-1500), chart.Points[0].Date);
        Assert.Equal(Today, chart.Points[^1].Date);
        Assert.Equal(chart.Points.Select(p => p.Date).Distinct().Count(), chart.Points.Count);
        Assert.Equal(chart.Points.Select(p => p.Date).OrderBy(d => d), chart.Points.Select(p => p.Date));
    }
}
