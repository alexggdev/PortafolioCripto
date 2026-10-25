using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CriptoPortfolio.Application.Abstractions;

namespace CriptoPortfolio.Infrastructure.Providers.Binance;

public sealed class BinanceMarketClient : IBinanceMarketClient
{
    private const string Quote = "USDT";

    // Mismo límite que la columna PriceUsd (decimal(28,18): 10 cifras enteras).
    private const decimal MaxPrice = 9_999_999_999m;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;

    public BinanceMarketClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetUsdtPricesAsync(CancellationToken ct = default)
    {
        // Una sola llamada devuelve todos los pares (peso 4 de un límite de 6.000 por minuto).
        using var response = await _http.GetAsync("ticker/price", ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var items = await JsonSerializer.DeserializeAsync<List<BinanceTickerPrice>>(stream, JsonOptions, ct)
                    ?? new List<BinanceTickerPrice>();

        var prices = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var symbol = item.Symbol;
            if (symbol is null
                || symbol.Length <= Quote.Length
                || !symbol.EndsWith(Quote, StringComparison.Ordinal))
            {
                continue;
            }

            // Binance manda el precio como texto: "83554.00000000".
            if (!decimal.TryParse(item.Price, NumberStyles.Float, CultureInfo.InvariantCulture, out var price)
                || price <= 0
                || price >= MaxPrice)
            {
                continue;
            }

            prices[symbol] = price;
        }

        return prices;
    }
}

internal sealed class BinanceTickerPrice
{
    [JsonPropertyName("symbol")] public string? Symbol { get; set; }

    [JsonPropertyName("price")] public string? Price { get; set; }
}
