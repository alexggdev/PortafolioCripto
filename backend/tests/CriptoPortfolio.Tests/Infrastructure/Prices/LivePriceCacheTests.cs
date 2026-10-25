using CriptoPortfolio.Application.Prices;
using CriptoPortfolio.Infrastructure.Prices;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Prices;

public class LivePriceCacheTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Starts_empty_and_misses()
    {
        var cache = new LivePriceCache();

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGet(1, out var price));
        Assert.Null(price);
    }

    [Fact]
    public void Set_then_get_returns_the_price()
    {
        var cache = new LivePriceCache();

        cache.SetRange(new[] { new KeyValuePair<int, LivePrice>(1, new LivePrice(100m, Now)) });

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet(1, out var price));
        Assert.Equal(100m, price!.PriceUsd);
        Assert.Equal(Now, price.UpdatedAtUtc);
    }

    [Fact]
    public void Set_overwrites_and_keeps_other_entries()
    {
        var cache = new LivePriceCache();
        cache.SetRange(new[]
        {
            new KeyValuePair<int, LivePrice>(1, new LivePrice(100m, Now)),
            new KeyValuePair<int, LivePrice>(2, new LivePrice(5m, Now))
        });

        cache.SetRange(new[] { new KeyValuePair<int, LivePrice>(1, new LivePrice(110m, Now.AddSeconds(5))) });

        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryGet(1, out var one));
        Assert.Equal(110m, one!.PriceUsd);
        Assert.True(cache.TryGet(2, out var two));
        Assert.Equal(5m, two!.PriceUsd);
    }

    [Fact]
    public void Snapshot_is_a_copy()
    {
        var cache = new LivePriceCache();
        cache.SetRange(new[] { new KeyValuePair<int, LivePrice>(1, new LivePrice(100m, Now)) });

        var snapshot = cache.Snapshot();
        cache.SetRange(new[] { new KeyValuePair<int, LivePrice>(2, new LivePrice(5m, Now)) });

        Assert.Single(snapshot);
        Assert.Equal(2, cache.Count);
    }
}
