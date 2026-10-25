using System.Globalization;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Catalog;
using CriptoPortfolio.Domain;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Sync;

public sealed class CatalogSyncService : ICatalogSyncService
{
    // Si el proveedor devuelve menos del 80 % de lo pedido, algo va mal (página caída, límite de llamadas):
    // en ese caso no se desactiva ninguna moneda, para no vaciar el catálogo por un fallo pasajero.
    private const double MinCoverageToDeactivate = 0.8;

    // Límites de las columnas decimal (ver AppDbContext). Un valor que no cabe hace fallar el INSERT entero
    // y se pierde toda la tanda. Las cifras absurdas del proveedor se descartan y se avisan en el log.
    private const decimal MaxPrice = 9_999_999_999m;                 // decimal(28,18): 10 cifras enteras
    private const decimal MaxMoney = 9_999_999_999_999_999_999m;     // decimal(28,8): 20 cifras enteras
    private const decimal MaxPercent = 99_999_999_999m;              // decimal(18,6): 12 cifras enteras

    private const int MaxSymbol = 32;
    private const int MaxName = 200;
    private const int MaxId = 200;
    private const int MaxImageUrl = 500;

    private readonly IAppDbContext _db;
    private readonly ICatalogProvider _provider;
    private readonly ISyncLockService _locks;
    private readonly TimeProvider _time;
    private readonly CatalogSyncOptions _options;
    private readonly ILogger<CatalogSyncService> _log;

    public CatalogSyncService(
        IAppDbContext db,
        ICatalogProvider provider,
        ISyncLockService locks,
        TimeProvider time,
        IOptions<CatalogSyncOptions> options,
        ILogger<CatalogSyncService> log)
    {
        _db = db;
        _provider = provider;
        _locks = locks;
        _time = time;
        _options = options.Value;
        _log = log;
    }

    public async Task<CatalogSyncResult> SyncAsync(string owner, CancellationToken ct = default)
    {
        var lease = TimeSpan.FromMinutes(Math.Max(1, _options.LeaseMinutes));

        if (!await _locks.TryAcquireAsync(SyncJobNames.Catalog, owner, lease, ct))
        {
            return CatalogSyncResult.Skipped();
        }

        var result = CatalogSyncResult.Empty();
        string? error = null;

        try
        {
            var coins = await _provider.GetTopCoinsAsync(_options.TopCount, ct);

            if (!await _locks.HeartbeatAsync(SyncJobNames.Catalog, owner, lease, ct))
            {
                error = "Se perdió el candado durante la sincronización.";
            }
            else
            {
                result = await ApplyAsync(coins, ct);
            }
        }
        catch (OperationCanceledException)
        {
            error = "Cancelado.";
            throw;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            _log.LogError(ex, "[WORKER] catalog: la sincronización falló");
        }
        finally
        {
            // CancellationToken.None: el candado debe soltarse aunque se esté apagando la aplicación.
            await _locks.ReleaseAsync(SyncJobNames.Catalog, owner, result.Processed, result.Failed, error,
                CancellationToken.None);
        }

        if (error is null)
        {
            _log.LogInformation(
                "[WORKER] catalog: {Processed} monedas ({Created} nuevas, {Deactivated} desactivadas, {Failed} con error)",
                result.Processed, result.Created, result.Deactivated, result.Failed);
        }

        return result with { Error = error };
    }

    private async Task<CatalogSyncResult> ApplyAsync(IReadOnlyList<CatalogCoin> coins, CancellationToken ct)
    {
        if (coins.Count == 0)
        {
            throw new InvalidOperationException("El proveedor no devolvió ninguna moneda.");
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var incomingIds = coins.Select(c => c.CoinGeckoId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existing = await _db.Assets
            .Include(a => a.Price)
            .Where(a => a.CoinGeckoId != null)
            .ToDictionaryAsync(a => a.CoinGeckoId!, StringComparer.OrdinalIgnoreCase, ct);

        var created = 0;
        var failed = 0;
        var touchedPrices = new List<(string CoinId, AssetPrice Row)>();

        foreach (var coin in coins)
        {
            if (coin.CoinGeckoId.Length > MaxId)
            {
                failed++;
                continue;
            }

            if (!existing.TryGetValue(coin.CoinGeckoId, out var asset))
            {
                asset = new Asset
                {
                    CoinGeckoId = coin.CoinGeckoId,
                    Slug = coin.CoinGeckoId.ToLowerInvariant(),
                    CreatedAtUtc = now
                };
                _db.Assets.Add(asset);
                created++;
            }

            asset.Symbol = Truncate(coin.Symbol, MaxSymbol);
            asset.Name = Truncate(coin.Name, MaxName);
            asset.ImageUrl = coin.ImageUrl is { Length: <= MaxImageUrl } ? coin.ImageUrl : null;
            asset.Rank = coin.Rank;
            asset.IsActive = true; // si volvió al top, se reactiva
            asset.UpdatedAtUtc = now;

            var dropped = false;
            var price = CleanMoney(coin.PriceUsd, MaxPrice, coin.CoinGeckoId, "precio", ref dropped);
            var marketCap = CleanMoney(coin.MarketCapUsd, MaxMoney, coin.CoinGeckoId, "capitalización", ref dropped);
            var volume = CleanMoney(coin.Volume24hUsd, MaxMoney, coin.CoinGeckoId, "volumen 24 h", ref dropped);
            var change1h = CleanPercent(coin.Change1hPct, coin.CoinGeckoId, "variación 1 h", ref dropped);
            var change24h = CleanPercent(coin.Change24hPct, coin.CoinGeckoId, "variación 24 h", ref dropped);
            var change7d = CleanPercent(coin.Change7dPct, coin.CoinGeckoId, "variación 7 d", ref dropped);

            if (dropped)
            {
                failed++;
            }

            if (price is { } validPrice)
            {
                var row = asset.Price ??= new AssetPrice();
                row.PriceUsd = validPrice;
                row.MarketCapUsd = marketCap;
                row.Volume24hUsd = volume;
                row.Change1hPct = change1h;
                row.Change24hPct = change24h;
                row.Change7dPct = change7d;
                row.Source = DataSource.CoinGecko;
                row.UpdatedAtUtc = now;
                touchedPrices.Add((coin.CoinGeckoId, row));
            }
        }

        var deactivated = 0;
        var coverage = coins.Count / (double)Math.Max(1, _options.TopCount);
        if (coverage >= MinCoverageToDeactivate)
        {
            foreach (var asset in existing.Values)
            {
                if (asset.IsActive && !incomingIds.Contains(asset.CoinGeckoId!))
                {
                    asset.IsActive = false; // las monedas no se borran nunca
                    asset.UpdatedAtUtc = now;
                    deactivated++;
                }
            }
        }
        else
        {
            _log.LogWarning(
                "[WORKER] catalog: el proveedor devolvió {Got} de {Wanted} monedas; no se desactiva ninguna",
                coins.Count, _options.TopCount);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            LogFailedBatch(ex, touchedPrices);
            throw;
        }

        return new CatalogSyncResult(true, coins.Count - failed, failed, created, deactivated, null);
    }

    private decimal? CleanMoney(decimal? value, decimal max, string coinId, string field, ref bool dropped)
    {
        if (value is null)
        {
            return null;
        }

        if (value < 0 || value >= max)
        {
            return Drop(coinId, field, value.Value, ref dropped);
        }

        return value;
    }

    private decimal? CleanPercent(decimal? value, string coinId, string field, ref bool dropped)
    {
        if (value is null)
        {
            return null;
        }

        if (Math.Abs(value.Value) >= MaxPercent)
        {
            return Drop(coinId, field, value.Value, ref dropped);
        }

        return value;
    }

    private decimal? Drop(string coinId, string field, decimal value, ref bool dropped)
    {
        dropped = true;
        _log.LogWarning("[WORKER] catalog: {Coin} trae un valor fuera de rango en {Field} ({Value}); se descarta",
            coinId, field, value);
        return null;
    }

    /// <summary>Si el guardado falla, deja en el log las filas del lote con sus valores y escalas para ver cuál sobra.</summary>
    private void LogFailedBatch(DbUpdateException ex, List<(string CoinId, AssetPrice Row)> touched)
    {
        var types = string.Join(", ", ex.Entries.Select(e => e.Entity.GetType().Name).Distinct());
        _log.LogError("[CATALOG] Falló el guardado de un lote de {Count} entidades ({Types}): {Message}",
            ex.Entries.Count, types, ex.InnerException?.Message ?? ex.Message);

        foreach (var (coinId, row) in touched)
        {
            if (!ex.Entries.Any(e => ReferenceEquals(e.Entity, row)))
            {
                continue;
            }

            _log.LogError(
                "[CATALOG] fila {Coin}: precio={Price} cap={Cap} vol={Vol} 1h={C1} 24h={C24} 7d={C7}",
                coinId, Show(row.PriceUsd), Show(row.MarketCapUsd), Show(row.Volume24hUsd),
                Show(row.Change1hPct), Show(row.Change24hPct), Show(row.Change7dPct));
        }
    }

    private static string Show(decimal? value) =>
        value is null ? "null" : $"{value.Value.ToString(CultureInfo.InvariantCulture)}[escala {value.Value.Scale}]";

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
