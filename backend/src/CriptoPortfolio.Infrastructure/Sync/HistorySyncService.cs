using System.Net;
using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.History;
using CriptoPortfolio.Domain;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Sync;

public sealed class HistorySyncService : IHistorySyncService
{
    private readonly IAppDbContext _db;
    private readonly IBinanceHistoryClient _binance;
    private readonly ICoinGeckoHistoryClient _gecko;
    private readonly ISyncLockService _locks;
    private readonly HistoryRetryGate _gate;
    private readonly TimeProvider _time;
    private readonly HistorySyncOptions _options;
    private readonly ILogger<HistorySyncService> _log;

    public HistorySyncService(
        IAppDbContext db,
        IBinanceHistoryClient binance,
        ICoinGeckoHistoryClient gecko,
        ISyncLockService locks,
        HistoryRetryGate gate,
        TimeProvider time,
        IOptions<HistorySyncOptions> options,
        ILogger<HistorySyncService> log)
    {
        _db = db;
        _binance = binance;
        _gecko = gecko;
        _locks = locks;
        _gate = gate;
        _time = time;
        _options = options.Value;
        _log = log;
    }

    // Datos de la moneda que hacen falta, sin cargar la entidad entera (y sin seguimiento).
    private sealed class AssetInfo
    {
        public int Id { get; init; }
        public string? CoinGeckoId { get; init; }
        public string? BinanceSymbol { get; init; }
        public DataSource HistorySource { get; init; }
        public decimal? PriceUsd { get; init; }
        public decimal? Volume24hUsd { get; init; }
        public DateTime? PriceUpdatedAtUtc { get; init; }
        public DataSource? PriceSource { get; init; }
    }

    // Estado de un proveedor durante una pasada.
    private sealed class Provider
    {
        public Provider(string name, int delayMs)
        {
            Name = name;
            DelayMs = delayMs;
        }

        public string Name { get; }
        public int DelayMs { get; }
        public bool Stopped { get; set; }
        public string? StopReason { get; set; }
        public int ConsecutiveFailures { get; set; }
    }

    public async Task<HistorySyncResult> SyncAsync(string owner, CancellationToken ct = default)
    {
        var lease = TimeSpan.FromMinutes(Math.Max(1, _options.LeaseMinutes));

        if (!await _locks.TryAcquireAsync(SyncJobNames.DailyHistory, owner, lease, ct))
        {
            return HistorySyncResult.Skipped();
        }

        var result = new HistorySyncResult(true, 0, 0, 0, 0, null);
        string? error = null;

        try
        {
            result = await RunAsync(owner, lease, ct);
            error = result.Error;
        }
        catch (OperationCanceledException)
        {
            error = "Cancelado.";
            throw;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            result = result with { Error = error };
            _log.LogError(ex, "[WORKER] daily-history: falló");
        }
        finally
        {
            await _locks.ReleaseAsync(SyncJobNames.DailyHistory, owner,
                result.Processed, result.Failed, error, CancellationToken.None);
        }

        if (result.Processed + result.Failed + result.Pending > 0)
        {
            _log.LogInformation(
                "[WORKER] daily-history: {Processed} monedas, {Inserted} días nuevos, {Failed} con error, {Pending} pendientes",
                result.Processed, result.Inserted, result.Failed, result.Pending);
        }

        return result;
    }

    private async Task<HistorySyncResult> RunAsync(string owner, TimeSpan lease, CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var yesterday = today.AddDays(-1);
        var todayStart = today.ToDateTime(TimeOnly.MinValue);
        var windowEnd = todayStart.AddMinutes(Math.Max(1, _options.SnapshotWindowMinutes));

        var assets = await LoadAssetsAsync(ct);
        var lastDates = await LoadLastDatesAsync(ct);

        var binance = new Provider("Binance", _options.BinanceDelayMs);
        var gecko = new Provider("CoinGecko", _options.CoinGeckoDelayMs);

        int processed = 0, inserted = 0, failed = 0, pending = 0;
        string? lostLock = null;

        // Primero las de Binance y después las de CoinGecko (el orden por ranking se mantiene dentro de cada grupo).
        foreach (var a in assets.OrderBy(x => x.BinanceSymbol is null ? 1 : 0))
        {
            var source = a.BinanceSymbol is not null ? DataSource.Binance : DataSource.CoinGecko;
            var provider = source == DataSource.Binance ? binance : gecko;
            var otherSource = source == DataSource.Binance ? DataSource.CoinGecko : DataSource.Binance;

            // Si quedan cierres de la otra fuente, se reconstruye todo con la fuente actual (una sola fuente por moneda).
            var hasOtherRows = lastDates.ContainsKey((a.Id, otherSource));
            DateOnly? last = !hasOtherRows && lastDates.TryGetValue((a.Id, source), out var d) ? d : null;

            if (!hasOtherRows && last is not null && last >= yesterday)
            {
                // Al día. Solo puede faltar anotar la fuente (p. ej. una moneda que perdió su par de Binance).
                if (a.HistorySource != source)
                {
                    var tracked = await _db.Assets.FirstAsync(x => x.Id == a.Id, ct);
                    tracked.HistorySource = source;
                    tracked.UpdatedAtUtc = now;
                    await _db.SaveChangesAsync(ct);
                    _db.ChangeTracker.Clear();
                }

                continue;
            }

            if (_gate.IsBlocked(a.Id, now))
            {
                continue; // falló o no tenía datos hace poco: no se insiste en cada pasada
            }

            if (provider.Stopped)
            {
                pending++;
                continue;
            }

            if (!await _locks.HeartbeatAsync(SyncJobNames.DailyHistory, owner, lease, ct))
            {
                lostLock = "Se perdió el candado a mitad de la pasada.";
                break;
            }

            try
            {
                var plan = await PlanAsync(a, source, last, today, yesterday, todayStart, windowEnd, now, ct);

                if (plan.Waiting)
                {
                    pending++;
                    continue;
                }

                var rows = plan.Rows;
                if (rows.Count == 0)
                {
                    _gate.Block(a.Id, now.AddHours(Math.Max(1, _options.EmptyRetryHours)));
                }
                else
                {
                    if (hasOtherRows)
                    {
                        await _db.AssetDailyPrices
                            .Where(x => x.AssetId == a.Id && x.Source != source)
                            .ExecuteDeleteAsync(ct);
                    }

                    _db.AssetDailyPrices.AddRange(rows.Select(r => new AssetDailyPrice
                    {
                        AssetId = a.Id,
                        DateUtc = r.DateUtc,
                        CloseUsd = r.CloseUsd,
                        OpenUsd = r.OpenUsd,
                        HighUsd = r.HighUsd,
                        LowUsd = r.LowUsd,
                        VolumeUsd = r.VolumeUsd,
                        Source = source
                    }));

                    if (a.HistorySource != source)
                    {
                        var tracked = await _db.Assets.FirstAsync(x => x.Id == a.Id, ct);
                        tracked.HistorySource = source;
                        tracked.UpdatedAtUtc = now;
                    }

                    await _db.SaveChangesAsync(ct);
                    inserted += rows.Count;
                }

                provider.ConsecutiveFailures = 0;
                processed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex) when (IsBlockingStatus(ex.StatusCode))
            {
                // Límite de llamadas, bloqueo de IP o restricción regional: insistir solo empeora las cosas.
                provider.Stopped = true;
                provider.StopReason = $"{provider.Name} respondió {(int)ex.StatusCode!.Value}";
                pending++;
                _log.LogWarning("[WORKER] daily-history: {Reason}; se detiene {Provider} en esta pasada",
                    provider.StopReason, provider.Name);
            }
            catch (Exception ex)
            {
                failed++;
                provider.ConsecutiveFailures++;
                _gate.Block(a.Id, now.AddMinutes(Math.Max(1, _options.FailureRetryMinutes)));
                _log.LogWarning(ex, "[WORKER] daily-history: falló la moneda {AssetId} ({Provider})", a.Id, provider.Name);

                if (provider.ConsecutiveFailures >= Math.Max(1, _options.MaxConsecutiveFailures))
                {
                    provider.Stopped = true;
                    provider.StopReason = $"{provider.Name} falló {provider.ConsecutiveFailures} veces seguidas";
                    _log.LogWarning("[WORKER] daily-history: {Reason}; se detiene en esta pasada", provider.StopReason);
                }
            }
            finally
            {
                _db.ChangeTracker.Clear(); // evita acumular cientos de miles de filas en memoria durante la carga inicial
            }
        }

        var problems = new List<string>();
        if (failed > 0)
        {
            problems.Add($"{failed} monedas con error");
        }

        foreach (var p in new[] { binance, gecko })
        {
            if (p.Stopped)
            {
                problems.Add(p.StopReason!);
            }
        }

        if (lostLock is not null)
        {
            problems.Add(lostLock);
        }

        return new HistorySyncResult(true, processed, inserted, failed, pending,
            problems.Count == 0 ? null : string.Join("; ", problems));
    }

    private sealed record Plan(IReadOnlyList<DailyPrice> Rows, bool Waiting);

    private async Task<Plan> PlanAsync(
        AssetInfo a, DataSource source, DateOnly? last, DateOnly today, DateOnly yesterday,
        DateTime todayStart, DateTime windowEnd, DateTime now, CancellationToken ct)
    {
        IReadOnlyList<DailyPrice> fetched;

        if (source == DataSource.Binance)
        {
            var from = last is null
                ? today.AddDays(-Math.Max(1, _options.BinanceInitialDays))
                : last.Value.AddDays(1);

            fetched = await _binance.GetDailyPricesAsync(a.BinanceSymbol!, from, ct);
            await PauseAsync(_options.BinanceDelayMs, ct);
        }
        else
        {
            var initialDays = Math.Max(1, _options.CoinGeckoInitialDays);
            int days;

            if (last is null)
            {
                days = initialDays;
            }
            else
            {
                var gap = yesterday.DayNumber - last.Value.DayNumber;

                if (gap == 1)
                {
                    // Falta solo el cierre de ayer: sale del precio que el catálogo guardó al empezar el día UTC.
                    var fresh = a.PriceUpdatedAtUtc is { } updated
                                && updated >= todayStart
                                && updated < windowEnd
                                && a.PriceSource == DataSource.CoinGecko
                                && a.PriceUsd is > 0;

                    if (fresh)
                    {
                        return new Plan(new[] { new DailyPrice(yesterday, a.PriceUsd!.Value, VolumeUsd: a.Volume24hUsd) }, false);
                    }

                    if (now < windowEnd)
                    {
                        return new Plan(Array.Empty<DailyPrice>(), true); // el catálogo aún no ha refrescado el precio del día
                    }
                }

                days = Math.Min(gap + 2, initialDays);
            }

            fetched = await _gecko.GetDailyPricesAsync(a.CoinGeckoId!, days, ct);
            await PauseAsync(_options.CoinGeckoDelayMs, ct);
        }

        // Solo días cerrados y posteriores al último guardado; una fecha como máximo.
        var rows = fetched
            .Where(r => r.DateUtc <= yesterday && (last is null || r.DateUtc > last))
            .GroupBy(r => r.DateUtc)
            .Select(g => g.First())
            .OrderBy(r => r.DateUtc)
            .ToList();

        return new Plan(rows, false);
    }

    private async Task<List<AssetInfo>> LoadAssetsAsync(CancellationToken ct) =>
        await _db.Assets
            .AsNoTracking()
            .Where(a => a.IsActive && (a.BinanceSymbol != null || a.CoinGeckoId != null))
            .OrderBy(a => a.Rank ?? int.MaxValue)
            .ThenBy(a => a.Id)
            .Select(a => new AssetInfo
            {
                Id = a.Id,
                CoinGeckoId = a.CoinGeckoId,
                BinanceSymbol = a.BinanceSymbol,
                HistorySource = a.HistorySource,
                PriceUsd = a.Price != null ? a.Price.PriceUsd : (decimal?)null,
                Volume24hUsd = a.Price != null ? a.Price.Volume24hUsd : (decimal?)null,
                PriceUpdatedAtUtc = a.Price != null ? a.Price.UpdatedAtUtc : (DateTime?)null,
                PriceSource = a.Price != null ? a.Price.Source : (DataSource?)null
            })
            .ToListAsync(ct);

    // Último día guardado por (moneda, fuente). Una sola consulta agrupada para todas las monedas.
    private async Task<Dictionary<(int AssetId, DataSource Source), DateOnly>> LoadLastDatesAsync(CancellationToken ct)
    {
        var rows = await _db.AssetDailyPrices
            .AsNoTracking()
            .GroupBy(d => new { d.AssetId, d.Source })
            .Select(g => new { g.Key.AssetId, g.Key.Source, Last = g.Max(d => d.DateUtc) })
            .ToListAsync(ct);

        return rows.ToDictionary(r => (r.AssetId, r.Source), r => r.Last);
    }

    private static bool IsBlockingStatus(HttpStatusCode? status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.Forbidden
            or HttpStatusCode.UnavailableForLegalReasons
            or (HttpStatusCode)418;

    private static Task PauseAsync(int ms, CancellationToken ct) =>
        ms > 0 ? Task.Delay(ms, ct) : Task.CompletedTask;
}
