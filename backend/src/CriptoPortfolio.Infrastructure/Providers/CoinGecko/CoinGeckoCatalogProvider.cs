using System.Text.Json;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Catalog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Providers.CoinGecko;

public sealed class CoinGeckoCatalogProvider : ICatalogProvider
{
    private const int MaxPageSize = 250;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly CoinGeckoOptions _options;
    private readonly ILogger<CoinGeckoCatalogProvider> _log;

    public CoinGeckoCatalogProvider(
        HttpClient http,
        IOptions<CoinGeckoOptions> options,
        ILogger<CoinGeckoCatalogProvider> log)
    {
        _http = http;
        _options = options.Value;
        _log = log;
    }

    public async Task<IReadOnlyList<CatalogCoin>> GetTopCoinsAsync(int count, CancellationToken ct = default)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "count debe ser al menos 1.");
        }

        var pageSize = Math.Clamp(_options.PageSize, 1, MaxPageSize);
        var pages = (int)Math.Ceiling(count / (double)pageSize);

        var result = new List<CatalogCoin>(count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var page = 1; page <= pages && result.Count < count; page++)
        {
            var items = await FetchPageAsync(page, pageSize, ct);

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Id)
                    || string.IsNullOrWhiteSpace(item.Symbol)
                    || string.IsNullOrWhiteSpace(item.Name))
                {
                    continue;
                }

                if (!seen.Add(item.Id))
                {
                    continue; // una moneda repetida entre páginas (el ranking se mueve entre llamadas)
                }

                result.Add(new CatalogCoin(
                    CoinGeckoId: item.Id,
                    Symbol: item.Symbol.Trim().ToUpperInvariant(),
                    Name: item.Name.Trim(),
                    ImageUrl: string.IsNullOrWhiteSpace(item.Image) ? null : item.Image,
                    Rank: item.MarketCapRank ?? result.Count + 1,
                    PriceUsd: item.CurrentPrice,
                    MarketCapUsd: item.MarketCap,
                    Volume24hUsd: item.TotalVolume,
                    Change1hPct: item.Change1h,
                    Change24hPct: item.Change24h,
                    Change7dPct: item.Change7d));

                if (result.Count == count)
                {
                    break;
                }
            }

            if (items.Count < pageSize)
            {
                break; // última página: no hay más monedas
            }
        }

        _log.LogInformation("[CATALOG] CoinGecko devolvió {Count} monedas", result.Count);
        return result;
    }

    private async Task<List<CoinGeckoMarketItem>> FetchPageAsync(int page, int pageSize, CancellationToken ct)
    {
        var url = $"coins/markets?vs_currency=usd&order=market_cap_desc&per_page={pageSize}&page={page}"
                  + "&sparkline=false&price_change_percentage=1h,24h,7d";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(_options.ApiKeyHeader, _options.ApiKey);
        }

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _log.LogWarning("[CATALOG] CoinGecko respondió {Status} en la página {Page}", (int)response.StatusCode, page);
            response.EnsureSuccessStatusCode(); // lanza HttpRequestException; el worker lo registra y reintenta en el siguiente ciclo
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var items = await JsonSerializer.DeserializeAsync<List<CoinGeckoMarketItem>>(stream, JsonOptions, ct);
        return items ?? new List<CoinGeckoMarketItem>();
    }
}
