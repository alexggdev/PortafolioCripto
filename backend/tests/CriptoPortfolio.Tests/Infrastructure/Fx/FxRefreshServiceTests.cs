using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Fx;
using CriptoPortfolio.Infrastructure.Fx;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Fx;

public class FxRefreshServiceTests
{
    private sealed class FakeClient : IFxRatesClient
    {
        public Func<FxFetch> Next { get; set; } = () => throw new InvalidOperationException("sin configurar");

        public Task<FxFetch> GetUsdRatesAsync(CancellationToken ct = default) => Task.FromResult(Next());
    }

    private readonly FakeClient _client = new();
    private readonly FxRateCache _cache = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero));

    private FxRefreshService Create() =>
        new(_client, _cache, _time, Options.Create(new FxOptions()), NullLogger<FxRefreshService>.Instance);

    private static FxFetch Fetch(decimal eur = 0.86m, decimal pen = 3.4m, decimal clp = 950m, bool includeClp = true)
    {
        var rates = new Dictionary<string, decimal> { ["EUR"] = eur, ["PEN"] = pen, ["JPY"] = 150m };
        if (includeClp)
        {
            rates["CLP"] = clp;
        }

        return new FxFetch(rates, new DateTime(2026, 10, 14, 0, 0, 1, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Stores_only_supported_currencies_with_usd_fixed_to_one()
    {
        _client.Next = () => Fetch();

        var result = await Create().RefreshAsync();

        Assert.True(result.Updated);
        var snapshot = Assert.IsType<FxSnapshot>(_cache.Current);
        Assert.Equal(new[] { "CLP", "EUR", "PEN", "USD" }, snapshot.PerUsd.Keys.OrderBy(k => k).ToArray());
        Assert.Equal(1m, snapshot.PerUsd["USD"]);
        Assert.Equal(0.86m, snapshot.PerUsd["EUR"]);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, snapshot.FetchedAtUtc);
        Assert.Equal(new DateTime(2026, 10, 14, 0, 0, 1, DateTimeKind.Utc), snapshot.ProviderUpdatedAtUtc);
    }

    [Fact]
    public async Task Rejects_a_response_missing_a_currency_and_keeps_the_previous_rates()
    {
        _client.Next = () => Fetch();
        await Create().RefreshAsync();
        var before = _cache.Current;

        _client.Next = () => Fetch(includeClp: false);
        var result = await Create().RefreshAsync();

        Assert.False(result.Updated);
        Assert.Contains("CLP", result.Error!);
        Assert.Same(before, _cache.Current);
    }

    [Fact]
    public async Task Rejects_a_suspicious_jump_and_keeps_the_previous_rates()
    {
        _client.Next = () => Fetch();
        await Create().RefreshAsync();
        var before = _cache.Current;

        _time.Advance(TimeSpan.FromHours(6));
        _client.Next = () => Fetch(clp: 95m); // −90 %
        var result = await Create().RefreshAsync();

        Assert.False(result.Updated);
        Assert.Contains("CLP", result.Error!);
        Assert.Same(before, _cache.Current);
    }

    [Fact]
    public async Task Accepts_a_normal_move()
    {
        _client.Next = () => Fetch();
        await Create().RefreshAsync();

        _time.Advance(TimeSpan.FromHours(6));
        _client.Next = () => Fetch(eur: 0.87m, clp: 960m);
        var result = await Create().RefreshAsync();

        Assert.True(result.Updated);
        Assert.Equal(960m, _cache.Current!.PerUsd["CLP"]);
    }

    [Fact]
    public async Task Skips_the_jump_check_when_the_previous_rates_are_very_old()
    {
        _client.Next = () => Fetch();
        await Create().RefreshAsync();

        _time.Advance(TimeSpan.FromDays(10));
        _client.Next = () => Fetch(clp: 95m);
        var result = await Create().RefreshAsync();

        Assert.True(result.Updated);
        Assert.Equal(95m, _cache.Current!.PerUsd["CLP"]);
    }

    [Fact]
    public async Task Returns_the_error_without_throwing_when_the_client_fails_and_keeps_the_previous_rates()
    {
        _client.Next = () => Fetch();
        await Create().RefreshAsync();
        var before = _cache.Current;

        _client.Next = () => throw new HttpRequestException("sin red");
        var result = await Create().RefreshAsync();

        Assert.False(result.Updated);
        Assert.Equal("sin red", result.Error);
        Assert.Same(before, _cache.Current);
    }

    [Fact]
    public async Task Does_not_swallow_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _client.Next = () => throw new OperationCanceledException(cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().RefreshAsync(cts.Token));
    }
}
