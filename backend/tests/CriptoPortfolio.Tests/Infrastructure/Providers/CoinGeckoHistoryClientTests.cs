using System.Net;
using System.Text;
using CriptoPortfolio.Infrastructure.Providers.CoinGecko;
using Microsoft.Extensions.Options;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Providers;

public class CoinGeckoHistoryClientTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_respond(request));
        }
    }

    private static CoinGeckoHistoryClient Create(FakeHandler handler, CoinGeckoOptions? options = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/api/v3/") },
            Options.Create(options ?? new CoinGeckoOptions()));

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static long Ms(int y, int m, int d, int h = 0, int min = 0) =>
        new DateTimeOffset(y, m, d, h, min, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    [Fact]
    public async Task Midnight_points_become_the_close_of_the_previous_day()
    {
        var json = $$"""
            {
              "prices": [[{{Ms(2026, 10, 11)}}, 100.5], [{{Ms(2026, 10, 12)}}, 105.25]],
              "market_caps": [[{{Ms(2026, 10, 11)}}, 1000], [{{Ms(2026, 10, 12)}}, 2000]],
              "total_volumes": [[{{Ms(2026, 10, 11)}}, 50.5], [{{Ms(2026, 10, 12)}}, 60]]
            }
            """;

        var prices = await Create(new FakeHandler(_ => Json(json))).GetDailyPricesAsync("bitcoin", 30);

        Assert.Equal(2, prices.Count);
        Assert.Equal(new DateOnly(2026, 10, 10), prices[0].DateUtc);
        Assert.Equal(100.5m, prices[0].CloseUsd);
        Assert.Equal(50.5m, prices[0].VolumeUsd);
        Assert.Equal(new DateOnly(2026, 10, 11), prices[1].DateUtc);
        Assert.Equal(105.25m, prices[1].CloseUsd);
        Assert.Null(prices[1].OpenUsd);
        Assert.Null(prices[1].HighUsd);
        Assert.Null(prices[1].LowUsd);
    }

    [Fact]
    public async Task Discards_the_mid_day_point_of_the_current_day()
    {
        var json = $$"""
            { "prices": [[{{Ms(2026, 10, 12)}}, 100], [{{Ms(2026, 10, 13)}}, 101], [{{Ms(2026, 10, 13, 9, 41)}}, 102]] }
            """;

        var prices = await Create(new FakeHandler(_ => Json(json))).GetDailyPricesAsync("bitcoin", 30);

        Assert.Equal(new[] { new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 12) }, prices.Select(p => p.DateUtc));
    }

    [Fact]
    public async Task Keeps_the_first_point_when_two_map_to_the_same_day()
    {
        var json = $$"""
            { "prices": [[{{Ms(2026, 10, 13)}}, 101], [{{Ms(2026, 10, 13, 0, 3)}}, 999]] }
            """;

        var prices = await Create(new FakeHandler(_ => Json(json))).GetDailyPricesAsync("bitcoin", 30);

        var p = Assert.Single(prices);
        Assert.Equal(101m, p.CloseUsd);
    }

    [Fact]
    public async Task Skips_invalid_prices_and_malformed_points()
    {
        var json = $$"""
            { "prices": [[{{Ms(2026, 10, 9)}}, 0], [{{Ms(2026, 10, 10)}}, -4], [{{Ms(2026, 10, 11)}}, null], [1], "x",
                         [{{Ms(2026, 10, 12)}}, 99999999999999], [{{Ms(2026, 10, 13)}}, 0.00000123]] }
            """;

        var prices = await Create(new FakeHandler(_ => Json(json))).GetDailyPricesAsync("shiba", 30);

        var p = Assert.Single(prices);
        Assert.Equal(new DateOnly(2026, 10, 12), p.DateUtc);
        Assert.Equal(0.00000123m, p.CloseUsd);
    }

    [Fact]
    public async Task Missing_prices_gives_empty_result()
    {
        var prices = await Create(new FakeHandler(_ => Json("""{"status":{"error_code":404}}"""))).GetDailyPricesAsync("x", 30);

        Assert.Empty(prices);
    }

    [Fact]
    public async Task Sends_days_daily_interval_escaped_id_and_the_api_key()
    {
        var handler = new FakeHandler(_ => Json("""{"prices":[]}"""));
        var client = Create(handler, new CoinGeckoOptions { ApiKey = "secreto", ApiKeyHeader = "x-cg-demo-api-key" });

        await client.GetDailyPricesAsync("bitcoin cash", 365);

        var request = handler.Requests.Single();
        var url = request.RequestUri!.AbsoluteUri;
        Assert.Contains("coins/bitcoin%20cash/market_chart", url);
        Assert.Contains("vs_currency=usd", url);
        Assert.Contains("days=365", url);
        Assert.Contains("interval=daily", url);
        Assert.Equal("secreto", request.Headers.GetValues("x-cg-demo-api-key").Single());
    }

    [Fact]
    public async Task Does_not_send_the_header_without_api_key()
    {
        var handler = new FakeHandler(_ => Json("""{"prices":[]}"""));

        await Create(handler).GetDailyPricesAsync("bitcoin", 30);

        Assert.False(handler.Requests.Single().Headers.Contains("x-cg-demo-api-key"));
    }

    [Fact]
    public async Task Rate_limit_throws_with_the_status_code()
    {
        var handler = new FakeHandler(_ => Json("{}", HttpStatusCode.TooManyRequests));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).GetDailyPricesAsync("bitcoin", 30));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Fact]
    public async Task Blank_id_is_rejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => Create(new FakeHandler(_ => Json("{}"))).GetDailyPricesAsync(" ", 30));
    }
}
