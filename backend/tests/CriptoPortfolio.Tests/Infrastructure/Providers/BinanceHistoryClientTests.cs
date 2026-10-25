using System.Net;
using System.Text;
using CriptoPortfolio.Infrastructure.Providers.Binance;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Providers;

public class BinanceHistoryClientTests
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

    // "Hoy" para los tests: 13 de octubre de 2026, a mediodía UTC.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));

    private BinanceHistoryClient Create(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/api/v3/") }, _time);

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static long Ms(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();

    // Una vela diaria de Binance: [apertura, open, high, low, close, volumen, cierre, volumenCotizado, ...]
    private static string Candle(DateTime dayUtc, string open = "100", string high = "120", string low = "90",
        string close = "110", string quoteVolume = "5000.5")
    {
        var openMs = Ms(dayUtc);
        var closeMs = openMs + 86_399_999;
        return $"[{openMs},\"{open}\",\"{high}\",\"{low}\",\"{close}\",\"10\",{closeMs},\"{quoteVolume}\",42,\"5\",\"500\",\"0\"]";
    }

    private static DateTime Day(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Maps_candles_and_uses_the_quote_volume()
    {
        var handler = new FakeHandler(_ => Json("[" + Candle(Day(2026, 10, 10), "100", "120", "90", "110", "5000.5") + "]"));

        var prices = await Create(handler).GetDailyPricesAsync("BTCUSDT", new DateOnly(2026, 10, 10));

        var p = Assert.Single(prices);
        Assert.Equal(new DateOnly(2026, 10, 10), p.DateUtc);
        Assert.Equal(110m, p.CloseUsd);
        Assert.Equal(100m, p.OpenUsd);
        Assert.Equal(120m, p.HighUsd);
        Assert.Equal(90m, p.LowUsd);
        Assert.Equal(5000.5m, p.VolumeUsd);
    }

    [Fact]
    public async Task Sends_symbol_interval_and_start_time()
    {
        var handler = new FakeHandler(_ => Json("[]"));

        await Create(handler).GetDailyPricesAsync("ETHUSDT", new DateOnly(2026, 10, 1));

        var url = handler.Requests.Single().RequestUri!.AbsoluteUri;
        Assert.Contains("klines?", url);
        Assert.Contains("symbol=ETHUSDT", url);
        Assert.Contains("interval=1d", url);
        Assert.Contains($"startTime={Ms(Day(2026, 10, 1))}", url);
        Assert.Contains("limit=1000", url);
    }

    [Fact]
    public async Task Excludes_the_candle_of_the_current_day()
    {
        var handler = new FakeHandler(_ => Json("[" + Candle(Day(2026, 10, 12)) + "," + Candle(Day(2026, 10, 13)) + "]"));

        var prices = await Create(handler).GetDailyPricesAsync("BTCUSDT", new DateOnly(2026, 10, 12));

        Assert.Equal(new DateOnly(2026, 10, 12), Assert.Single(prices).DateUtc);
    }

    [Fact]
    public async Task Skips_candles_with_invalid_close_and_nulls_invalid_optional_fields()
    {
        var handler = new FakeHandler(_ => Json("[" +
            Candle(Day(2026, 10, 8), close: "0") + "," +
            Candle(Day(2026, 10, 9), close: "abc") + "," +
            Candle(Day(2026, 10, 10), high: "-1", quoteVolume: "x") + "]"));

        var prices = await Create(handler).GetDailyPricesAsync("BTCUSDT", new DateOnly(2026, 10, 8));

        var p = Assert.Single(prices);
        Assert.Equal(new DateOnly(2026, 10, 10), p.DateUtc);
        Assert.Null(p.HighUsd);
        Assert.Null(p.VolumeUsd);
    }

    [Fact]
    public async Task Paginates_when_a_page_is_full()
    {
        var first = Enumerable.Range(0, 1000).Select(i => Candle(Day(2022, 1, 1).AddDays(i)));
        var second = new[] { Candle(Day(2022, 1, 1).AddDays(1000)), Candle(Day(2022, 1, 1).AddDays(1001)) };
        var pages = new Queue<string>(new[] { "[" + string.Join(",", first) + "]", "[" + string.Join(",", second) + "]" });
        var handler = new FakeHandler(_ => Json(pages.Dequeue()));

        var prices = await Create(handler).GetDailyPricesAsync("BTCUSDT", new DateOnly(2022, 1, 1));

        Assert.Equal(1002, prices.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains($"startTime={Ms(Day(2022, 1, 1).AddDays(1000))}", handler.Requests[1].RequestUri!.AbsoluteUri);
        Assert.Equal(prices.Select(p => p.DateUtc).Distinct().Count(), prices.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("btcusdt")]
    [InlineData("BTC&limit=1")]
    [InlineData("BTCUSDT/../x")]
    public async Task Rejects_invalid_symbols(string symbol)
    {
        var handler = new FakeHandler(_ => Json("[]"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => Create(handler).GetDailyPricesAsync(symbol, new DateOnly(2026, 10, 1)));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Http_error_throws()
    {
        var handler = new FakeHandler(_ => Json("{}", HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Create(handler).GetDailyPricesAsync("BTCUSDT", new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public async Task Unexpected_json_shape_throws()
    {
        var handler = new FakeHandler(_ => Json("""{"code":-1121,"msg":"Invalid symbol."}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Create(handler).GetDailyPricesAsync("BTCUSDT", new DateOnly(2026, 10, 1)));
    }
}
