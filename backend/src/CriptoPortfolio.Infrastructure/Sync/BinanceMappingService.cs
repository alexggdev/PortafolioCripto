using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Prices;
using CriptoPortfolio.Domain;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Infrastructure.Prices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Sync;

public sealed class BinanceMappingService : IBinanceMappingService
{
    private const string Quote = "USDT";

    private readonly IAppDbContext _db;
    private readonly IBinanceMarketClient _client;
    private readonly ISyncLockService _locks;
    private readonly TimeProvider _time;
    private readonly LivePriceOptions _options;
    private readonly ILogger<BinanceMappingService> _log;

    public BinanceMappingService(
        IAppDbContext db,
        IBinanceMarketClient client,
        ISyncLockService locks,
        TimeProvider time,
        IOptions<LivePriceOptions> options,
        ILogger<BinanceMappingService> log)
    {
        _db = db;
        _client = client;
        _locks = locks;
        _time = time;
        _options = options.Value;
        _log = log;
    }

    public async Task<BinanceMappingResult> SyncAsync(string owner, CancellationToken ct = default)
    {
        var lease = TimeSpan.FromMinutes(Math.Max(1, _options.MappingLeaseMinutes));

        if (!await _locks.TryAcquireAsync(SyncJobNames.BinanceMapping, owner, lease, ct))
        {
            return new BinanceMappingResult(false, 0, 0, 0, null);
        }

        var result = new BinanceMappingResult(true, 0, 0, 0, null);
        string? error = null;

        try
        {
            var pairs = await _client.GetUsdtPricesAsync(ct);
            result = await ApplyAsync(pairs, ct);
        }
        catch (OperationCanceledException)
        {
            error = "Cancelado.";
            throw;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            _log.LogError(ex, "[WORKER] binance-mapping: falló");
        }
        finally
        {
            await _locks.ReleaseAsync(SyncJobNames.BinanceMapping, owner,
                result.Assigned + result.Released, result.Rejected, error, CancellationToken.None);
        }

        if (error is null && (result.Assigned + result.Released + result.Rejected) > 0)
        {
            _log.LogInformation(
                "[WORKER] binance-mapping: {Assigned} asignadas, {Released} liberadas, {Rejected} rechazadas",
                result.Assigned, result.Released, result.Rejected);
        }

        return result with { Error = error };
    }

    private async Task<BinanceMappingResult> ApplyAsync(IReadOnlyDictionary<string, decimal> pairs, CancellationToken ct)
    {
        if (pairs.Count == 0)
        {
            throw new InvalidOperationException("Binance no devolvió ningún par contra USDT.");
        }

        var now = _time.GetUtcNow().UtcDateTime;

        // Las activas, y las inactivas que todavía conservan un par (para poder liberarlo).
        var assets = await _db.Assets
            .Include(a => a.Price)
            .Where(a => a.IsActive || a.BinanceSymbol != null)
            .ToListAsync(ct);

        // Varias monedas pueden compartir símbolo: solo la de mejor ranking se queda con el par.
        var desired = new Dictionary<int, string>();
        var winners = assets
            .Where(a => a.IsActive)
            .GroupBy(a => a.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(a => a.Rank ?? int.MaxValue).ThenBy(a => a.Id).First());

        foreach (var winner in winners)
        {
            var pair = winner.Symbol.ToUpperInvariant() + Quote;
            if (pairs.ContainsKey(pair))
            {
                desired[winner.Id] = pair;
            }
        }

        // Paso 1: quitar los pares que ya no corresponden. Va en un guardado aparte para que el índice único
        // de BinanceSymbol no choque si un par pasa de una moneda a otra.
        var released = 0;
        foreach (var asset in assets)
        {
            if (asset.BinanceSymbol is null)
            {
                continue;
            }

            if (!desired.TryGetValue(asset.Id, out var wanted) || wanted != asset.BinanceSymbol)
            {
                asset.BinanceSymbol = null;
                asset.UpdatedAtUtc = now;
                released++;
            }
        }

        if (released > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        // Paso 2: asignar los nuevos, comprobando que el precio cuadra con el de CoinGecko.
        var assigned = 0;
        var rejected = 0;
        foreach (var asset in assets)
        {
            if (!desired.TryGetValue(asset.Id, out var pair) || asset.BinanceSymbol == pair)
            {
                continue;
            }

            var binancePrice = pairs[pair];
            if (asset.Price is not null && asset.Price.PriceUsd > 0)
            {
                var deviation = Math.Abs(binancePrice - asset.Price.PriceUsd) / asset.Price.PriceUsd;
                if (deviation > _options.MaxPriceDeviation)
                {
                    rejected++;
                    _log.LogWarning(
                        "[WORKER] binance-mapping: {Coin} no se liga a {Pair}: Binance {Binance} frente a CoinGecko {Gecko}",
                        asset.CoinGeckoId, pair, binancePrice, asset.Price.PriceUsd);
                    continue;
                }
            }

            if (asset.BinanceSymbol is null)
            {
                asset.HistorySource = DataSource.Binance; // el histórico diario saldrá de Binance (una sola fuente por moneda)
            }

            asset.BinanceSymbol = pair;
            asset.UpdatedAtUtc = now;
            assigned++;
        }

        if (assigned > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return new BinanceMappingResult(true, assigned, released, rejected, null);
    }
}
