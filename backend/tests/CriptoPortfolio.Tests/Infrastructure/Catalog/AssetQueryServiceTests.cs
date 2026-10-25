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

public class AssetQueryServiceTests : IDisposable
{
    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
    private readonly LivePriceCache _cache = new();

    public void Dispose() => _db.Dispose();

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private async Task<int> AddAsync(string slug, string symbol, string name, int? rank, decimal? price = 1m,
        bool active = true, decimal? change24h = null, decimal? marketCap = null)
    {
        await using var context = _db.CreateContext();
        var asset = new Asset
        {
            Slug = slug,
            Symbol = symbol,
            Name = name,
            CoinGeckoId = slug,
            Rank = rank,
            IsActive = active,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        if (price is not null)
        {
            asset.Price = new AssetPrice
            {
                PriceUsd = price.Value,
                Change24hPct = change24h,
                MarketCapUsd = marketCap,
                Source = DataSource.CoinGecko,
                UpdatedAtUtc = Now.AddMinutes(-5)
            };
        }

        context.Assets.Add(asset);
        await context.SaveChangesAsync();
        return asset.Id;
    }

    private AssetQueryService Service()
    {
        var context = _db.CreateContext();
        return new AssetQueryService(context, _cache, _time, Options.Create(new LivePriceOptions { StaleSeconds = 60 }));
    }

    [Fact]
    public async Task Lists_only_active_assets_ordered_by_rank_with_unranked_last()
    {
        await AddAsync("c", "C", "Charlie", null);
        await AddAsync("b", "B", "Bravo", 2);
        await AddAsync("a", "A", "Alpha", 1);
        await AddAsync("gone", "G", "Gone", 3, active: false);

        var result = await Service().ListAsync(new AssetListQuery());

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(new[] { "a", "b", "c" }, result.Items.Select(i => i.Slug));
    }

    [Fact]
    public async Task Search_matches_name_or_symbol_ignoring_case()
    {
        await AddAsync("bitcoin", "BTC", "Bitcoin", 1);
        await AddAsync("ethereum", "ETH", "Ethereum", 2);
        await AddAsync("bitcoin-cash", "BCH", "Bitcoin Cash", 3);

        var byName = await Service().ListAsync(new AssetListQuery(Search: "bitco"));
        var bySymbol = await Service().ListAsync(new AssetListQuery(Search: "eth"));

        Assert.Equal(new[] { "bitcoin", "bitcoin-cash" }, byName.Items.Select(i => i.Slug));
        Assert.Equal(new[] { "ethereum" }, bySymbol.Items.Select(i => i.Slug));
    }

    [Fact]
    public async Task Search_treats_wildcards_as_plain_text()
    {
        await AddAsync("bitcoin", "BTC", "Bitcoin", 1);

        var result = await Service().ListAsync(new AssetListQuery(Search: "%"));

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Sorts_by_price_descending()
    {
        await AddAsync("low", "L", "Low", 1, price: 0.5m);
        await AddAsync("high", "H", "High", 2, price: 90000m);
        await AddAsync("mid", "M", "Mid", 3, price: 20m);

        var result = await Service().ListAsync(new AssetListQuery(Sort: AssetSort.Price, Descending: true));

        Assert.Equal(new[] { "high", "mid", "low" }, result.Items.Select(i => i.Slug));
    }

    [Fact]
    public async Task Sorts_by_change24h_and_market_cap()
    {
        await AddAsync("a", "A", "A", 1, change24h: -5m, marketCap: 100m);
        await AddAsync("b", "B", "B", 2, change24h: 12m, marketCap: 900m);
        await AddAsync("c", "C", "C", 3, change24h: 3m, marketCap: 500m);

        var byChange = await Service().ListAsync(new AssetListQuery(Sort: AssetSort.Change24h, Descending: true));
        var byCap = await Service().ListAsync(new AssetListQuery(Sort: AssetSort.MarketCap, Descending: true));

        Assert.Equal(new[] { "b", "c", "a" }, byChange.Items.Select(i => i.Slug));
        Assert.Equal(new[] { "b", "c", "a" }, byCap.Items.Select(i => i.Slug));
    }

    [Fact]
    public async Task Paginates_and_reports_totals()
    {
        for (var i = 1; i <= 5; i++)
        {
            await AddAsync($"coin-{i}", $"C{i}", $"Coin {i}", i);
        }

        var second = await Service().ListAsync(new AssetListQuery(Page: 2, PageSize: 2));

        Assert.Equal(5, second.TotalCount);
        Assert.Equal(3, second.TotalPages);
        Assert.Equal(2, second.Page);
        Assert.Equal(new[] { "coin-3", "coin-4" }, second.Items.Select(i => i.Slug));
    }

    [Fact]
    public async Task Out_of_range_paging_values_are_corrected()
    {
        await AddAsync("a", "A", "A", 1);

        var result = await Service().ListAsync(new AssetListQuery(Page: 0, PageSize: 100_000));

        Assert.Equal(1, result.Page);
        Assert.Equal(AssetListQuery.MaxPageSize, result.PageSize);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Uses_the_live_price_when_it_is_fresh()
    {
        var id = await AddAsync("btc", "BTC", "Bitcoin", 1, price: 80000m);
        _cache.SetRange(new[] { new KeyValuePair<int, LivePrice>(id, new LivePrice(81000m, Now.AddSeconds(-10))) });

        var item = (await Service().ListAsync(new AssetListQuery())).Items.Single();

        Assert.Equal(81000m, item.PriceUsd);
        Assert.Equal(PriceSources.Live, item.PriceSource);
        Assert.Equal(Now.AddSeconds(-10), item.PriceUpdatedAtUtc);
    }

    [Fact]
    public async Task Falls_back_to_the_sql_price_when_the_live_price_is_stale()
    {
        var id = await AddAsync("btc", "BTC", "Bitcoin", 1, price: 80000m);
        _cache.SetRange(new[] { new KeyValuePair<int, LivePrice>(id, new LivePrice(81000m, Now.AddMinutes(-10))) });

        var item = (await Service().ListAsync(new AssetListQuery())).Items.Single();

        Assert.Equal(80000m, item.PriceUsd);
        Assert.Equal(PriceSources.Delayed, item.PriceSource);
    }

    [Fact]
    public async Task Falls_back_to_the_sql_price_when_the_cache_is_empty()
    {
        await AddAsync("btc", "BTC", "Bitcoin", 1, price: 80000m);

        var item = (await Service().ListAsync(new AssetListQuery())).Items.Single();

        Assert.Equal(80000m, item.PriceUsd);
        Assert.Equal(PriceSources.Delayed, item.PriceSource);
        Assert.Equal(Now.AddMinutes(-5), item.PriceUpdatedAtUtc);
    }

    [Fact]
    public async Task Asset_without_any_price_has_null_price_fields()
    {
        await AddAsync("new", "NEW", "New", 1, price: null);

        var item = (await Service().ListAsync(new AssetListQuery())).Items.Single();

        Assert.Null(item.PriceUsd);
        Assert.Null(item.PriceSource);
        Assert.Null(item.PriceUpdatedAtUtc);
    }

    [Fact]
    public async Task Get_by_slug_returns_the_asset_ignoring_case()
    {
        await AddAsync("bitcoin", "BTC", "Bitcoin", 1, price: 80000m);

        var item = await Service().GetBySlugAsync("  Bitcoin ");

        Assert.NotNull(item);
        Assert.Equal("BTC", item!.Symbol);
        Assert.Equal(80000m, item.PriceUsd);
    }

    [Fact]
    public async Task Get_by_slug_returns_null_for_missing_or_inactive()
    {
        await AddAsync("gone", "G", "Gone", 1, active: false);

        Assert.Null(await Service().GetBySlugAsync("gone"));
        Assert.Null(await Service().GetBySlugAsync("nope"));
        Assert.Null(await Service().GetBySlugAsync(" "));
    }
}
