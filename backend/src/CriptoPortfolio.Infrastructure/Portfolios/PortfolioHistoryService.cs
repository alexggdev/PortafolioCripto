using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Catalog;
using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Domain;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Domain.History;
using CriptoPortfolio.Infrastructure.Prices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Portfolios;

/// <summary>
/// Historial del portafolio. Las fotos diarias (PortfolioDailyValues) son una caché de la reconstrucción:
/// si están completas, la gráfica sale de la tabla; si falta alguna, se reconstruye todo desde las transacciones
/// y se guardan los días que faltan. Los precios de la gráfica salen de AssetDailyPrices (SQL); ninguna petición
/// de usuario llama a un proveedor externo.
/// </summary>
public sealed class PortfolioHistoryService : IPortfolioHistoryService, IPortfolioHistoryInvalidator
{
    private readonly IAppDbContext _db;
    private readonly ILivePriceCache _cache;
    private readonly ISyncLockService _locks;
    private readonly TimeProvider _time;
    private readonly LivePriceOptions _live;
    private readonly PortfolioHistoryOptions _options;
    private readonly ILogger<PortfolioHistoryService> _log;

    public PortfolioHistoryService(
        IAppDbContext db,
        ILivePriceCache cache,
        ISyncLockService locks,
        TimeProvider time,
        IOptions<LivePriceOptions> live,
        IOptions<PortfolioHistoryOptions> options,
        ILogger<PortfolioHistoryService> log)
    {
        _db = db;
        _cache = cache;
        _locks = locks;
        _time = time;
        _live = live.Value;
        _options = options.Value;
        _log = log;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------ invalidación

    public async Task StageInvalidationAsync(Guid portfolioId, DateTime fromUtc, CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(fromUtc);
        var stale = await _db.PortfolioDailyValues
            .Where(v => v.PortfolioId == portfolioId && v.DateUtc >= from)
            .ToListAsync(ct);
        _db.PortfolioDailyValues.RemoveRange(stale);
    }

    // ----------------------------------------------------------------- lectura

    public async Task<PortfolioHistory> GetHistoryAsync(
        Guid userId, ChartRange range, Guid? portfolioId = null, CancellationToken ct = default)
    {
        var code = ChartRanges.Code(range);

        var owned = _db.Portfolios.AsNoTracking().Where(p => p.UserId == userId);
        var portfolio = portfolioId is { } wanted
            ? await owned.FirstOrDefaultAsync(p => p.Id == wanted, ct)
            : await owned.OrderBy(p => p.CreatedAtUtc).ThenBy(p => p.Id).FirstOrDefaultAsync(ct);
        if (portfolio is null)
        {
            return new PortfolioHistory(null, code, null, Array.Empty<PortfolioHistoryPoint>(), null, false);
        }

        var transactions = await LoadTransactionsAsync(portfolio.Id, ct);
        if (transactions.Count == 0)
        {
            return new PortfolioHistory(portfolio.Id, code, null, Array.Empty<PortfolioHistoryPoint>(), null, false);
        }

        var today = DateOnly.FromDateTime(Now);
        var first = DateOnly.FromDateTime(transactions.Min(t => t.DateUtc));
        var yesterday = today.AddDays(-1);
        var expected = Math.Max(0, yesterday.DayNumber - first.DayNumber + 1);

        var stored = await _db.PortfolioDailyValues.AsNoTracking()
            .CountAsync(v => v.PortfolioId == portfolio.Id && v.DateUtc >= first && v.DateUtc <= yesterday, ct);

        List<HistoryDay> days;
        if (stored == expected)
        {
            // Fotos completas: se leen de la tabla y solo se calcula el punto de hoy.
            var rows = await _db.PortfolioDailyValues.AsNoTracking()
                .Where(v => v.PortfolioId == portfolio.Id && v.DateUtc >= first && v.DateUtc <= yesterday)
                .OrderBy(v => v.DateUtc)
                .Select(v => new { v.DateUtc, v.ValueUsd, v.NetContributedUsd })
                .ToListAsync(ct);

            days = rows.Select(r => new HistoryDay(r.DateUtc, r.ValueUsd, r.NetContributedUsd)).ToList();
            var prices = await PricesForAsync(transactions, today, closesUpTo: null, ct);
            days.AddRange(PortfolioHistoryCalculator.Compute(transactions, prices.Prices, today, today));
        }
        else
        {
            days = (await RebuildAsync(portfolio.Id, transactions, first, today, ct)).ToList();
        }

        return Shape(portfolio.Id, range, first, days, today);
    }

    private PortfolioHistory Shape(Guid portfolioId, ChartRange range, DateOnly first, List<HistoryDay> days, DateOnly today)
    {
        DateOnly from = ChartRanges.Days(range) is { } n ? today.AddDays(-n) : first;
        var visible = days.Where(d => d.Date >= from).ToList();

        decimal? profit = null;
        if (visible.Count >= 2)
        {
            var a = visible[0];
            var b = visible[^1];
            profit = (b.ValueUsd - a.ValueUsd) - (b.NetContributedUsd - a.NetContributedUsd);
        }

        var points = Downsample(visible.Select(d => new PortfolioHistoryPoint(d.Date, d.ValueUsd, d.NetContributedUsd)).ToList());
        var includesCurrent = days.Count > 0 && days[^1].Date == today;
        return new PortfolioHistory(portfolioId, ChartRanges.Code(range), first, points, profit, includesCurrent);
    }

    // --------------------------------------------------------- reconstrucción

    /// <summary>
    /// Reproduce todo el historial, guarda los días que falten (los ya completos de ayer hacia atrás) y devuelve la serie
    /// desde la primera transacción hasta hoy. Si dos peticiones lo hacen a la vez, una puede chocar al guardar: no pasa nada,
    /// la serie ya está calculada y la otra petición guardó los mismos días.
    /// </summary>
    private async Task<IReadOnlyList<HistoryDay>> RebuildAsync(
        Guid portfolioId, List<Transaction> transactions, DateOnly first, DateOnly today, CancellationToken ct)
    {
        var yesterday = today.AddDays(-1);
        var (prices, book) = await PricesForAsync(transactions, today, closesUpTo: yesterday, ct);
        var days = PortfolioHistoryCalculator.Compute(transactions, prices, first, today);

        var existing = (await _db.PortfolioDailyValues.AsNoTracking()
                .Where(v => v.PortfolioId == portfolioId)
                .Select(v => v.DateUtc)
                .ToListAsync(ct))
            .ToHashSet();

        var settledTo = SettledTo(transactions, book, yesterday);
        var toAdd = days
            .Where(d => d.Date <= settledTo && d.Date <= yesterday && !existing.Contains(d.Date))
            .Select(d => new PortfolioDailyValue
            {
                PortfolioId = portfolioId,
                DateUtc = d.Date,
                ValueUsd = d.ValueUsd,
                NetContributedUsd = d.NetContributedUsd
            })
            .ToList();

        // Fotos de antes de la primera transacción (si se borró la más antigua): ya no valen.
        var orphans = existing.Where(d => d < first).ToList();
        if (orphans.Count > 0)
        {
            var rows = await _db.PortfolioDailyValues
                .Where(v => v.PortfolioId == portfolioId && v.DateUtc < first)
                .ToListAsync(ct);
            _db.PortfolioDailyValues.RemoveRange(rows);
        }

        if (toAdd.Count > 0 || orphans.Count > 0)
        {
            _db.PortfolioDailyValues.AddRange(toAdd);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                _log.LogInformation(ex, "[API] Fotos del portafolio {PortfolioId}: otra petición guardó los mismos días", portfolioId);
                _db.ChangeTracker.Clear();
            }
        }

        return days;
    }

    /// <summary>
    /// Último día que ya se puede dar por definitivo: el menor entre ayer y el último cierre de cada moneda que
    /// tiene histórico y no está estancada.
    /// </summary>
    private DateOnly SettledTo(List<Transaction> transactions, DailyPriceBook book, DateOnly yesterday)
    {
        var settled = yesterday;
        foreach (var assetId in transactions.Select(t => t.AssetId).Distinct())
        {
            if (book.LastDate(assetId) is not { } last)
            {
                continue; // sin histórico: se valora con el precio de las transacciones del usuario
            }

            if (yesterday.DayNumber - last.DayNumber > Math.Max(0, _options.StalePriceDays))
            {
                continue; // estancada: no bloquea al resto
            }

            if (last < settled)
            {
                settled = last;
            }
        }

        return settled;
    }

    // ----------------------------------------------------------------- precios

    private async Task<List<Transaction>> LoadTransactionsAsync(Guid portfolioId, CancellationToken ct) =>
        await _db.Transactions.AsNoTracking().Where(t => t.PortfolioId == portfolioId).ToListAsync(ct);

    /// <summary>
    /// Precios para calcular: cierres diarios (todos hasta <paramref name="closesUpTo"/>; si es null, solo las monedas
    /// con transferencias sin valorar, que necesitan el cierre del día de la transferencia) y, para hoy, el precio actual.
    /// </summary>
    private async Task<(IDailyPrices Prices, DailyPriceBook Book)> PricesForAsync(
        List<Transaction> transactions, DateOnly today, DateOnly? closesUpTo, CancellationToken ct)
    {
        var assetIds = transactions.Select(t => t.AssetId).Distinct().ToList();

        var closeIds = closesUpTo is null
            ? transactions
                .Where(t => t.UnitPriceUsd <= 0m && t.Type is TransactionType.TransferIn or TransactionType.TransferOut)
                .Select(t => t.AssetId).Distinct().ToList()
            : assetIds;

        var book = new DailyPriceBook();
        if (closeIds.Count > 0)
        {
            var limit = closesUpTo ?? today;
            var rows = await _db.AssetDailyPrices.AsNoTracking()
                .Where(d => closeIds.Contains(d.AssetId) && d.DateUtc <= limit)
                .Select(d => new { d.AssetId, d.DateUtc, d.CloseUsd })
                .ToListAsync(ct);

            foreach (var group in rows.GroupBy(r => r.AssetId))
            {
                book.Add(group.Key, new DailyCloseSeries(
                    group.Select(r => new KeyValuePair<DateOnly, decimal>(r.DateUtc, r.CloseUsd))));
            }
        }

        var assets = await _db.Assets.AsNoTracking()
            .Include(a => a.Price)
            .Where(a => assetIds.Contains(a.Id))
            .ToListAsync(ct);

        var now = Now;
        var current = new Dictionary<int, decimal>();
        foreach (var asset in assets)
        {
            if (_cache.TryGet(asset.Id, out var live)
                && now - live.UpdatedAtUtc <= TimeSpan.FromSeconds(Math.Max(1, _live.StaleSeconds)))
            {
                current[asset.Id] = live.PriceUsd;
            }
            else if (asset.Price is { PriceUsd: > 0m } price)
            {
                current[asset.Id] = price.PriceUsd;
            }
        }

        return (new OverlayPrices(book, today, current), book);
    }

    // -------------------------------------------------------------- worker

    public async Task<PortfolioSnapshotResult> SnapshotAllAsync(string owner, CancellationToken ct = default)
    {
        var lease = TimeSpan.FromMinutes(Math.Max(1, _options.LeaseMinutes));
        if (!await _locks.TryAcquireAsync(SyncJobNames.PortfolioSnapshots, owner, lease, ct))
        {
            return PortfolioSnapshotResult.Skipped();
        }

        var portfolios = 0;
        var rebuilt = 0;
        var failed = 0;
        string? error = null;

        try
        {
            var ids = await _db.Transactions.AsNoTracking().Select(t => t.PortfolioId).Distinct().ToListAsync(ct);
            var today = DateOnly.FromDateTime(Now);
            var yesterday = today.AddDays(-1);

            foreach (var id in ids)
            {
                ct.ThrowIfCancellationRequested();
                portfolios++;
                try
                {
                    var transactions = await LoadTransactionsAsync(id, ct);
                    if (transactions.Count > 0)
                    {
                        var first = DateOnly.FromDateTime(transactions.Min(t => t.DateUtc));
                        var expected = Math.Max(0, yesterday.DayNumber - first.DayNumber + 1);
                        var stored = await _db.PortfolioDailyValues.AsNoTracking()
                            .CountAsync(v => v.PortfolioId == id && v.DateUtc >= first && v.DateUtc <= yesterday, ct);
                        if (stored != expected)
                        {
                            await RebuildAsync(id, transactions, first, today, ct);
                            rebuilt++;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    _log.LogError(ex, "[WORKER] portfolio-snapshots: falló el portafolio {PortfolioId}", id);
                }
                finally
                {
                    _db.ChangeTracker.Clear();
                }

                if (portfolios % 50 == 0 && !await _locks.HeartbeatAsync(SyncJobNames.PortfolioSnapshots, owner, lease, ct))
                {
                    error = "Se perdió el candado.";
                    break;
                }
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
            _log.LogError(ex, "[WORKER] portfolio-snapshots: falló");
        }
        finally
        {
            await _locks.ReleaseAsync(SyncJobNames.PortfolioSnapshots, owner, portfolios, failed, error, CancellationToken.None);
        }

        if (rebuilt + failed > 0)
        {
            _log.LogInformation("[WORKER] portfolio-snapshots: {Portfolios} portafolios, {Rebuilt} completados, {Failed} con error",
                portfolios, rebuilt, failed);
        }

        return new PortfolioSnapshotResult(true, portfolios, rebuilt, failed);
    }

    // ------------------------------------------------------------------ ayudas

    private IReadOnlyList<PortfolioHistoryPoint> Downsample(List<PortfolioHistoryPoint> points)
    {
        var max = Math.Max(2, _options.MaxPoints);
        if (points.Count <= max)
        {
            return points;
        }

        var result = new List<PortfolioHistoryPoint>(max);
        var last = points.Count - 1;
        var previous = -1;
        for (var i = 0; i < max; i++)
        {
            var index = (int)Math.Round(i * (double)last / (max - 1));
            if (index != previous)
            {
                result.Add(points[index]);
                previous = index;
            }
        }

        return result;
    }
}
