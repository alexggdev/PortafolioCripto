using System.Net;
using System.Text;
using CriptoPortfolio.Infrastructure.Providers.ExchangeRate;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Providers;

public class ExchangeRateClientTests
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

    private static ExchangeRateClient Create(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://fx.example.test/v6/") });

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Parses_rates_and_update_time()
    {
        var handler = new FakeHandler(_ => Json("""
            {"result":"success","base_code":"USD","time_last_update_unix":1760400000,
             "rates":{"USD":1,"EUR":0.8612,"PEN":3.41,"CLP":951.25}}
            """));

        var fetch = await Create(handler).GetUsdRatesAsync();

        Assert.Equal(0.8612m, fetch.PerUsd["EUR"]);
        Assert.Equal(3.41m, fetch.PerUsd["PEN"]);
        Assert.Equal(951.25m, fetch.PerUsd["CLP"]);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1760400000).UtcDateTime, fetch.ProviderUpdatedAtUtc);
        Assert.Equal("/v6/latest/USD", handler.Requests.Single().RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Drops_invalid_rates()
    {
        var handler = new FakeHandler(_ => Json("""
            {"result":"success","base_code":"USD","rates":{"EUR":0.86,"AAA":0,"BBB":-2,"CCC":99999999999,"LONGCODE":5}}
            """));

        var fetch = await Create(handler).GetUsdRatesAsync();

        Assert.Single(fetch.PerUsd);
        Assert.Null(fetch.ProviderUpdatedAtUtc);
    }

    [Fact]
    public async Task Throws_when_the_provider_reports_an_error_inside_a_200()
    {
        var handler = new FakeHandler(_ => Json("""{"result":"error","error-type":"quota-reached"}"""));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).GetUsdRatesAsync());

        Assert.Contains("quota-reached", ex.Message);
    }

    [Fact]
    public async Task Throws_when_the_base_is_not_usd()
    {
        var handler = new FakeHandler(_ => Json("""{"result":"success","base_code":"EUR","rates":{"USD":1.1}}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler).GetUsdRatesAsync());
    }

    [Fact]
    public async Task Throws_on_http_errors()
    {
        var handler = new FakeHandler(_ => Json("{}", HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).GetUsdRatesAsync());
    }
}
