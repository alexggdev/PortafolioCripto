using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Prices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Prices;

/// <summary>
/// Cada pocos segundos lee los precios de Binance y llena la caché en memoria. Cada instancia del backend
/// tiene su propia caché, así que no usa candado. El mapa moneda-par sí se mantiene con candado en SQL.
/// </summary>
public sealed class LivePriceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILivePriceCache _cache;
    private readonly TimeProvider _time;
    private readonly LivePriceOptions _options;
    private readonly ILogger<LivePriceWorker> _log;

    public LivePriceWorker(
        IServiceScopeFactory scopes,
        ILivePriceCache cache,
        TimeProvider time,
        IOptions<LivePriceOptions> options,
        ILogger<LivePriceWorker> log)
    {
        _scopes = scopes;
        _cache = cache;
        _time = time;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var owner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.IntervalSeconds));
        var mappingEvery = TimeSpan.FromMinutes(Math.Max(1, _options.MappingMinutes));

        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextMapping = DateTimeOffset.MinValue;
        var failures = 0;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.StartupDelaySeconds)), stoppingToken);

            using var timer = new PeriodicTimer(interval);
            do
            {
                try
                {
                    var now = _time.GetUtcNow();
                    if (now >= nextMapping)
                    {
                        map = await RefreshMappingAsync(owner, stoppingToken);
                        nextMapping = now + mappingEvery;
                        _log.LogInformation("[WORKER] live-prices: {Count} monedas con precio en vivo", map.Count);
                    }

                    await PollAsync(map, stoppingToken);

                    if (failures > 0)
                    {
                        _log.LogInformation("[WORKER] live-prices: Binance responde otra vez");
                        failures = 0;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failures++;
                    if (failures == 1 || failures % 12 == 0)
                    {
                        _log.LogWarning(ex,
                            "[WORKER] live-prices: fallo ({Failures} seguidos); se conservan los últimos precios",
                            failures);
                    }

                    // Espera extra creciente (hasta 30 s) para no machacar a un servidor caído o que nos bloquea.
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, failures * 5)), stoppingToken);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // apagado normal
        }
    }

    private async Task<Dictionary<string, int>> RefreshMappingAsync(string owner, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();

        var mapping = scope.ServiceProvider.GetRequiredService<IBinanceMappingService>();
        var result = await mapping.SyncAsync(owner, ct);
        if (result.Error is not null)
        {
            _log.LogWarning("[WORKER] binance-mapping: terminó con error: {Error}", result.Error);
        }

        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var rows = await db.Assets
            .Where(a => a.IsActive && a.BinanceSymbol != null)
            .Select(a => new { Symbol = a.BinanceSymbol!, a.Id })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Symbol, r => r.Id, StringComparer.Ordinal);
    }

    private async Task PollAsync(Dictionary<string, int> map, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IBinanceMarketClient>();

        var prices = await client.GetUsdtPricesAsync(ct);
        var now = _time.GetUtcNow().UtcDateTime;

        var batch = new List<KeyValuePair<int, LivePrice>>(map.Count);
        foreach (var (symbol, assetId) in map)
        {
            if (prices.TryGetValue(symbol, out var price))
            {
                batch.Add(new KeyValuePair<int, LivePrice>(assetId, new LivePrice(price, now)));
            }
        }

        _cache.SetRange(batch);
    }
}
