using System.Globalization;
using System.Text.Json;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.History;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Providers.CoinGecko;

public sealed class CoinGeckoHistoryClient : ICoinGeckoHistoryClient
{
    private const decimal MaxPrice = 9_999_999_999m;
    private const decimal MaxVolume = 9_999_999_999_999_999_999m;

    // Con interval=daily, CoinGecko da un punto a las 00:00 UTC de cada día (con unos segundos de margen)
    // y, al final, un punto "ahora mismo" del día en curso.
    private static readonly TimeSpan MidnightTolerance = TimeSpan.FromMinutes(10);

    private readonly HttpClient _http;
    private readonly CoinGeckoOptions _options;

    public CoinGeckoHistoryClient(HttpClient http, IOptions<CoinGeckoOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<DailyPrice>> GetDailyPricesAsync(
        string coinGeckoId, int days, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(coinGeckoId))
        {
            throw new ArgumentException("Falta el id de CoinGecko.", nameof(coinGeckoId));
        }

        days = Math.Max(1, days);
        var url = $"coins/{Uri.EscapeDataString(coinGeckoId)}/market_chart?vs_currency=usd&days={days}&interval=daily";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(_options.ApiKeyHeader, _options.ApiKey);
        }

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode(); // 429 y demás: el servicio decide si espera o reintenta

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("prices", out var prices)
            || prices.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<DailyPrice>();
        }

        var volumes = new Dictionary<long, decimal>();
        if (doc.RootElement.TryGetProperty("total_volumes", out var vols) && vols.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vols.EnumerateArray())
            {
                if (TryPoint(v, out var ts, out var volume) && volume >= 0 && volume < MaxVolume)
                {
                    volumes[ts] = volume;
                }
            }
        }

        var result = new List<DailyPrice>();
        var seen = new HashSet<DateOnly>();

        foreach (var p in prices.EnumerateArray())
        {
            if (!TryPoint(p, out var ts, out var price) || price <= 0 || price >= MaxPrice)
            {
                continue;
            }

            var moment = DateTimeOffset.FromUnixTimeMilliseconds(ts).UtcDateTime;
            if (moment.TimeOfDay >= MidnightTolerance)
            {
                continue; // el punto de "ahora" del día en curso: no es un cierre
            }

            // El precio de las 00:00 UTC del día D es el cierre del día D-1.
            var date = DateOnly.FromDateTime(moment).AddDays(-1);
            if (!seen.Add(date))
            {
                continue;
            }

            result.Add(new DailyPrice(
                date,
                price,
                VolumeUsd: volumes.TryGetValue(ts, out var vol) ? vol : null));
        }

        result.Sort((a, b) => a.DateUtc.CompareTo(b.DateUtc));
        return result;
    }

    private static bool TryPoint(JsonElement e, out long timestampMs, out decimal value)
    {
        timestampMs = 0;
        value = 0;

        if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() < 2)
        {
            return false;
        }

        var t = e[0];
        var v = e[1];
        if (t.ValueKind != JsonValueKind.Number || v.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        if (!t.TryGetInt64(out timestampMs))
        {
            // CoinGecko a veces manda el timestamp como 1704067200000.0
            timestampMs = (long)t.GetDouble();
        }

        // Se lee del texto original para no perder precisión con double.
        return decimal.TryParse(v.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
