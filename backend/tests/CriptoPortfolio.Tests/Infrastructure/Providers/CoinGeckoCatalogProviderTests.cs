using System.Net;
using System.Text;
using System.Text.Json;
using CriptoPortfolio.Infrastructure.Providers.CoinGecko;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Providers;

public class CoinGeckoCatalogProviderTests
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

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static CoinGeckoCatalogProvider Create(FakeHandler handler, CoinGeckoOptions? options = null)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/api/v3/") };
        return new CoinGeckoCatalogProvider(
            client,
            Options.Create(options ?? new CoinGeckoOptions()),
            NullLogger<CoinGeckoCatalogProvider>.Instance);
    }

    // Genera una página con 'count' monedas con ranking from, from+1, ...
    private static string Page(int from, int count) =>
        JsonSerializer.Serialize(Enumerable.Range(from, count).Select(i => new Dictionary<string, object?>
        {
            ["id"] = $"coin-{i}",
            ["symbol"] = $"c{i}",
            ["name"] = $"Coin {i}",
            ["image"] = "https://img.example.test/c.png",
            ["market_cap_rank"] = i,
            ["current_price"] = 1.5m,
            ["market_cap"] = 1000m,
            ["total_volume"] = 10m,
            ["price_change_percentage_1h_in_currency"] = 0.1m,
            ["price_change_percentage_24h_in_currency"] = -2.5m,
            ["price_change_percentage_7d_in_currency"] = 7.25m
        }));

    [Fact]
    public async Task Maps_every_field_and_uppercases_the_symbol()
    {
        const string json = """
            [{"id":"bitcoin","symbol":"btc","name":"Bitcoin","image":"https://img.example.test/btc.png",
              "current_price":98765.43,"market_cap":1950000000000,"market_cap_rank":1,"total_volume":45000000000,
              "price_change_percentage_1h_in_currency":0.12,
              "price_change_percentage_24h_in_currency":-1.5,
              "price_change_percentage_7d_in_currency":4.75}]
            """;
        var provider = Create(new FakeHandler(_ => JsonResponse(json)));

        var coins = await provider.GetTopCoinsAsync(1);

        var btc = Assert.Single(coins);
        Assert.Equal("bitcoin", btc.CoinGeckoId);
        Assert.Equal("BTC", btc.Symbol);
        Assert.Equal("Bitcoin", btc.Name);
        Assert.Equal("https://img.example.test/btc.png", btc.ImageUrl);
        Assert.Equal(1, btc.Rank);
        Assert.Equal(98765.43m, btc.PriceUsd);
        Assert.Equal(1_950_000_000_000m, btc.MarketCapUsd);
        Assert.Equal(45_000_000_000m, btc.Volume24hUsd);
        Assert.Equal(0.12m, btc.Change1hPct);
        Assert.Equal(-1.5m, btc.Change24hPct);
        Assert.Equal(4.75m, btc.Change7dPct);
    }

    [Fact]
    public async Task Asks_for_one_page_per_250_coins_and_trims_to_the_requested_count()
    {
        var handler = new FakeHandler(request =>
            request.RequestUri!.Query.Contains("page=1") ? JsonResponse(Page(1, 250)) : JsonResponse(Page(251, 250)));
        var provider = Create(handler);

        var coins = await provider.GetTopCoinsAsync(300);

        Assert.Equal(300, coins.Count);
        Assert.Equal(300, coins[^1].Rank);
        Assert.Equal(2, handler.Requests.Count);

        var first = handler.Requests[0].RequestUri!.Query;
        Assert.Contains("vs_currency=usd", first);
        Assert.Contains("per_page=250", first);
        Assert.Contains("page=1", first);
        Assert.Contains("page=2", handler.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task Stops_asking_when_a_page_comes_back_short()
    {
        var handler = new FakeHandler(_ => JsonResponse(Page(1, 100)));
        var provider = Create(handler);

        var coins = await provider.GetTopCoinsAsync(500);

        Assert.Equal(100, coins.Count);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Tolerates_nulls_exponents_and_missing_fields()
    {
        const string json = """
            [{"id":"tiny","symbol":"tiny","name":"Tiny","current_price":1.2E-8,"market_cap":null,"market_cap_rank":null}]
            """;
        var provider = Create(new FakeHandler(_ => JsonResponse(json)));

        var coin = Assert.Single(await provider.GetTopCoinsAsync(10));

        Assert.Equal(0.000000012m, coin.PriceUsd);
        Assert.Null(coin.MarketCapUsd);
        Assert.Null(coin.Volume24hUsd);
        Assert.Null(coin.Change7dPct);
        Assert.Equal(1, coin.Rank); // sin ranking del proveedor: se usa la posición
    }

    [Fact]
    public async Task Skips_incomplete_entries_and_duplicates()
    {
        const string json = """
            [{"id":"a","symbol":"a","name":"A"},
             {"symbol":"x","name":"Sin id"},
             {"id":"b","symbol":"","name":"Sin símbolo"},
             {"id":"a","symbol":"a","name":"A repetida"},
             {"id":"c","symbol":"c","name":"C"}]
            """;
        var provider = Create(new FakeHandler(_ => JsonResponse(json)));

        var coins = await provider.GetTopCoinsAsync(10);

        Assert.Equal(new[] { "a", "c" }, coins.Select(c => c.CoinGeckoId));
    }

    [Fact]
    public async Task Sends_the_api_key_in_the_configured_header()
    {
        var handler = new FakeHandler(_ => JsonResponse("[]"));
        var provider = Create(handler, new CoinGeckoOptions { ApiKey = "clave-123" });

        await provider.GetTopCoinsAsync(1);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("clave-123", Assert.Single(request.Headers.GetValues("x-cg-demo-api-key")));
    }

    [Fact]
    public async Task Sends_no_key_header_when_none_is_configured()
    {
        var handler = new FakeHandler(_ => JsonResponse("[]"));
        var provider = Create(handler);

        await provider.GetTopCoinsAsync(1);

        Assert.False(Assert.Single(handler.Requests).Headers.Contains("x-cg-demo-api-key"));
    }

    [Fact]
    public async Task Throws_on_http_errors_such_as_rate_limiting()
    {
        var provider = Create(new FakeHandler(_ => JsonResponse("{}", HttpStatusCode.TooManyRequests)));

        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetTopCoinsAsync(10));
    }

    [Fact]
    public async Task Rejects_a_non_positive_count()
    {
        var provider = Create(new FakeHandler(_ => JsonResponse("[]")));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.GetTopCoinsAsync(0));
    }
}
