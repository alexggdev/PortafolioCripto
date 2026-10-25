using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Fx;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Fx;

public sealed class FxRefreshService : IFxRefreshService
{
    private readonly IFxRatesClient _client;
    private readonly IFxRateCache _cache;
    private readonly TimeProvider _time;
    private readonly FxOptions _options;
    private readonly ILogger<FxRefreshService> _log;

    public FxRefreshService(
        IFxRatesClient client,
        IFxRateCache cache,
        TimeProvider time,
        IOptions<FxOptions> options,
        ILogger<FxRefreshService> log)
    {
        _client = client;
        _cache = cache;
        _time = time;
        _options = options.Value;
        _log = log;
    }

    public async Task<FxRefreshResult> RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var fetched = await _client.GetUsdRatesAsync(ct);

            var rates = new Dictionary<string, decimal>(StringComparer.Ordinal) { [FxCurrencies.Base] = 1m };
            foreach (var code in FxCurrencies.Quoted)
            {
                if (!fetched.PerUsd.TryGetValue(code, out var rate) || rate <= 0)
                {
                    return Reject($"falta el tipo de cambio de {code}");
                }

                rates[code] = rate;
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var previous = _cache.Current;

            if (previous is not null && now - previous.FetchedAtUtc <= TimeSpan.FromDays(Math.Max(1, _options.IgnoreCheckAfterDays)))
            {
                foreach (var code in FxCurrencies.Quoted)
                {
                    var before = previous.PerUsd[code];
                    var change = Math.Abs(rates[code] - before) / before;
                    if (change > _options.MaxChangeFraction)
                    {
                        return Reject($"{code} cambió un {change:P0} de golpe ({before} → {rates[code]})");
                    }
                }
            }

            _cache.Set(new FxSnapshot(rates, now, fetched.ProviderUpdatedAtUtc));
            return new FxRefreshResult(true, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[WORKER] fx-rates: no se pudieron obtener los tipos de cambio; se conservan los anteriores");
            return new FxRefreshResult(false, ex.Message);
        }
    }

    private FxRefreshResult Reject(string reason)
    {
        _log.LogWarning("[WORKER] fx-rates: respuesta descartada ({Reason}); se conservan los anteriores", reason);
        return new FxRefreshResult(false, reason);
    }
}
