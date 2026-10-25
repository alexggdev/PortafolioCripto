using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.History;

namespace CriptoPortfolio.Infrastructure.Providers.Binance;

public sealed partial class BinanceHistoryClient : IBinanceHistoryClient
{
    private const int PageLimit = 1000;
    private const int MaxPages = 20; // tope de seguridad: 20.000 días
    private const long DayMs = 86_400_000;

    // Mismos límites que las columnas decimal(28,18) de precios y decimal(28,8) de volumen.
    private const decimal MaxPrice = 9_999_999_999m;
    private const decimal MaxVolume = 9_999_999_999_999_999_999m;

    [GeneratedRegex("^[A-Z0-9]{2,20}$")]
    private static partial Regex SymbolPattern();

    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public BinanceHistoryClient(HttpClient http, TimeProvider time)
    {
        _http = http;
        _time = time;
    }

    public async Task<IReadOnlyList<DailyPrice>> GetDailyPricesAsync(
        string symbol, DateOnly from, CancellationToken ct = default)
    {
        if (!SymbolPattern().IsMatch(symbol ?? ""))
        {
            throw new ArgumentException("Símbolo de Binance no válido.", nameof(symbol));
        }

        var nowMs = _time.GetUtcNow().ToUnixTimeMilliseconds();
        var startMs = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();

        var result = new List<DailyPrice>();

        for (var page = 0; page < MaxPages; page++)
        {
            var url = $"klines?symbol={symbol}&interval=1d&startTime={startMs}&limit={PageLimit}";

            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Binance devolvió un formato inesperado en klines.");
            }

            var count = 0;
            long lastOpenMs = startMs;

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                count++;

                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 8)
                {
                    continue;
                }

                var openMs = row[0].GetInt64();
                var closeMs = row[6].GetInt64();
                lastOpenMs = openMs;

                if (closeMs >= nowMs)
                {
                    continue; // vela del día en curso: todavía no está cerrada
                }

                var close = ParsePrice(row[4]);
                if (close is null)
                {
                    continue;
                }

                result.Add(new DailyPrice(
                    DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(openMs).UtcDateTime),
                    close.Value,
                    ParsePrice(row[1]),
                    ParsePrice(row[2]),
                    ParsePrice(row[3]),
                    ParseVolume(row[7]))); // índice 7: volumen en la moneda de cotización (USDT)
            }

            if (count < PageLimit)
            {
                break;
            }

            startMs = lastOpenMs + DayMs;
            if (startMs >= nowMs)
            {
                break;
            }
        }

        return result;
    }

    private static decimal? ParsePrice(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.String
            || !decimal.TryParse(e.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || value <= 0
            || value >= MaxPrice)
        {
            return null;
        }

        return value;
    }

    private static decimal? ParseVolume(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.String
            || !decimal.TryParse(e.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || value < 0
            || value >= MaxVolume)
        {
            return null;
        }

        return value;
    }
}
