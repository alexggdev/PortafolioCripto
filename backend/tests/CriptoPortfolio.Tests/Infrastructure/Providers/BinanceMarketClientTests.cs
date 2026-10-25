using System.Net;
using System.Text;
using CriptoPortfolio.Infrastructure.Providers.Binance;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Providers;

public class BinanceMarketClientTests
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

    private static BinanceMarketClient Create(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.test/api/v3/") });

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Parses_string_prices_and_keeps_only_usdt_pairs()
    {
        var handler = new FakeHandler(_ => Json("""
            [
              {"symbol":"BTCUSDT","price":"83554.12000000"},
              {"symbol":"ETHUSDT","price":"1850.5"},
              {"symbol":"ETHBTC","price":"0.02200000"},
              {"symbol":"BNBBUSD","price":"600.00"}
            ]
            """));

        var prices = await Create(handler).GetUsdtPricesAsync();

        Assert.Equal(2, prices.Count);
        Assert.Equal(83554.12m, prices["BTCUSDT"]);
        Assert.Equal(1850.5m, prices["ETHUSDT"]);
        Assert.Equal("/api/v3/ticker/price", handler.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Ignores_the_bare_usdt_symbol_and_invalid_prices()
    {
        var handler = new FakeHandler(_ => Json("""
            [
              {"symbol":"USDT","price":"1"},
              {"symbol":"AAAUSDT","price":"0.00000000"},
              {"symbol":"BBBUSDT","price":"-3"},
              {"symbol":"CCCUSDT","price":"abc"},
              {"symbol":"DDDUSDT","price":null},
              {"symbol":"EEEUSDT","price":"99999999999999"},
              {"symbol":null,"price":"5"},
              {"symbol":"OKUSDT","price":"0.00001234"}
            ]
            """));

        var prices = await Create(handler).GetUsdtPricesAsync();

        Assert.Single(prices);
        Assert.Equal(0.00001234m, prices["OKUSDT"]);
    }

    [Fact]
    public async Task Empty_array_gives_empty_result()
    {
        var prices = await Create(new FakeHandler(_ => Json("[]"))).GetUsdtPricesAsync();

        Assert.Empty(prices);
    }

    [Fact]
    public async Task Http_error_throws()
    {
        var handler = new FakeHandler(_ => Json("{}", HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).GetUsdtPricesAsync());
    }
}
