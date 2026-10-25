using System.Text.Json;
using System.Text.Json.Serialization;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Fx;

namespace CriptoPortfolio.Infrastructure.Providers.ExchangeRate;

public sealed class ExchangeRateClient : IFxRatesClient
{
    // Tope de cordura: ninguna moneda real vale más de esto por dólar.
    private const decimal MaxRate = 10_000_000m;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;

    public ExchangeRateClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<FxFetch> GetUsdRatesAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync("latest/USD", ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var body = await JsonSerializer.DeserializeAsync<ExchangeRateResponse>(stream, JsonOptions, ct);

        // El proveedor devuelve 200 con "result":"error" en algunos fallos.
        if (body is null || !string.Equals(body.Result, "success", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"El proveedor de tipos de cambio respondió: {body?.ErrorType ?? "sin datos"}.");
        }

        if (!string.Equals(body.BaseCode, FxCurrencies.Base, StringComparison.OrdinalIgnoreCase) || body.Rates is null)
        {
            throw new InvalidOperationException("El proveedor de tipos de cambio no devolvió la base USD.");
        }

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, rate) in body.Rates)
        {
            if (code.Length == 3 && rate > 0 && rate < MaxRate)
            {
                rates[code.ToUpperInvariant()] = rate;
            }
        }

        DateTime? updated = body.TimeLastUpdateUnix is > 0
            ? DateTimeOffset.FromUnixTimeSeconds(body.TimeLastUpdateUnix.Value).UtcDateTime
            : null;

        return new FxFetch(rates, updated);
    }
}

internal sealed class ExchangeRateResponse
{
    [JsonPropertyName("result")] public string? Result { get; set; }

    [JsonPropertyName("error-type")] public string? ErrorType { get; set; }

    [JsonPropertyName("base_code")] public string? BaseCode { get; set; }

    [JsonPropertyName("time_last_update_unix")] public long? TimeLastUpdateUnix { get; set; }

    [JsonPropertyName("rates")] public Dictionary<string, decimal>? Rates { get; set; }
}
