using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Application.Alerts;
using CriptoPortfolio.Domain;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CriptoPortfolio.Infrastructure.Alerts;

public sealed class PriceAlertEvaluator : IPriceAlertEvaluator
{
    private readonly IAppDbContext _db;
    private readonly AlertPriceReader _prices;
    private readonly ISyncLockService _locks;
    private readonly TimeProvider _time;
    private readonly PriceAlertOptions _options;
    private readonly ILogger<PriceAlertEvaluator> _log;

    public PriceAlertEvaluator(
        IAppDbContext db,
        AlertPriceReader prices,
        ISyncLockService locks,
        TimeProvider time,
        IOptions<PriceAlertOptions> options,
        ILogger<PriceAlertEvaluator> log)
    {
        _db = db;
        _prices = prices;
        _locks = locks;
        _time = time;
        _options = options.Value;
        _log = log;
    }

    public async Task<AlertEvaluationResult> EvaluateAsync(string owner, CancellationToken ct = default)
    {
        var lease = TimeSpan.FromSeconds(Math.Max(10, _options.LeaseSeconds));
        if (!await _locks.TryAcquireAsync(SyncJobNames.PriceAlerts, owner, lease, ct))
        {
            return AlertEvaluationResult.Skipped();
        }

        var checkedCount = 0;
        var triggered = 0;
        var failed = 0;
        string? error = null;

        try
        {
            var now = _time.GetUtcNow().UtcDateTime;

            // Las ya disparadas se limpian solas pasado un tiempo.
            var cutoff = now.AddDays(-Math.Max(1, _options.KeepTriggeredDays));
            await _db.PriceAlerts
                .Where(a => a.TriggeredAtUtc != null && a.TriggeredAtUtc < cutoff)
                .ExecuteDeleteAsync(ct);

            var active = await _db.PriceAlerts.Where(a => a.TriggeredAtUtc == null).ToListAsync(ct);
            checkedCount = active.Count;

            if (active.Count > 0)
            {
                var prices = await _prices.GetPricesAsync(active.Select(a => a.AssetId).Distinct().ToList(), ct);
                foreach (var alert in active)
                {
                    if (prices.TryGetValue(alert.AssetId, out var price) && IsMet(alert, price))
                    {
                        alert.TriggeredAtUtc = now;
                        alert.TriggeredPriceUsd = price;
                        triggered++;
                    }
                }

                if (triggered > 0)
                {
                    await _db.SaveChangesAsync(ct);
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
            failed = 1;
            error = $"{ex.GetType().Name}: {ex.Message}";
            _log.LogError(ex, "[WORKER] price-alerts: falló la pasada");
        }
        finally
        {
            _db.ChangeTracker.Clear();
            await _locks.ReleaseAsync(SyncJobNames.PriceAlerts, owner, checkedCount, failed, error, CancellationToken.None);
        }

        if (triggered > 0)
        {
            _log.LogInformation("[WORKER] price-alerts: {Triggered} alertas disparadas de {Checked} activas", triggered, checkedCount);
        }

        return new AlertEvaluationResult(true, checkedCount, triggered);
    }

    public static bool IsMet(PriceAlert alert, decimal price) =>
        alert.Direction == AlertDirection.Above ? price >= alert.TargetPriceUsd : price <= alert.TargetPriceUsd;
}
