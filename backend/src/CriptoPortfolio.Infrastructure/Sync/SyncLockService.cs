using CriptoPortfolio.Application.Abstractions;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CriptoPortfolio.Infrastructure.Sync;

/// <summary>
/// Candado (lease) con latido sobre la tabla SyncStates. Tomar, alargar y soltar son UPDATE de una
/// sola sentencia con condición en el WHERE: la atomicidad la da la base de datos, no el código.
/// </summary>
public sealed class SyncLockService : ISyncLockService
{
    private const int MaxErrorLength = 2000;

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<SyncLockService> _log;

    public SyncLockService(AppDbContext db, TimeProvider time, ILogger<SyncLockService> log)
    {
        _db = db;
        _time = time;
        _log = log;
    }

    public async Task<bool> TryAcquireAsync(string jobName, string owner, TimeSpan lease, CancellationToken ct = default)
    {
        await EnsureRowAsync(jobName, ct);

        DateTime now = _time.GetUtcNow().UtcDateTime;
        DateTime? nowValue = now;
        DateTime? expires = now + lease;

        var rows = await _db.SyncStates
            .Where(s => s.Name == jobName
                        && (s.LeaseOwner == null
                            || s.LeaseOwner == owner
                            || s.LeaseExpiresAtUtc == null
                            || s.LeaseExpiresAtUtc < now))
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.LeaseOwner, owner)
                .SetProperty(s => s.LeaseExpiresAtUtc, expires)
                .SetProperty(s => s.HeartbeatAtUtc, nowValue)
                .SetProperty(s => s.LastStartedAtUtc, nowValue), ct);

        var acquired = rows == 1;
        if (acquired)
        {
            _log.LogDebug("[WORKER] {Job}: candado tomado por {Owner}", jobName, owner);
        }
        else
        {
            _log.LogInformation("[WORKER] {Job}: candado ocupado por otra instancia, se omite esta ejecución", jobName);
        }

        return acquired;
    }

    public async Task<bool> HeartbeatAsync(string jobName, string owner, TimeSpan lease, CancellationToken ct = default)
    {
        DateTime now = _time.GetUtcNow().UtcDateTime;
        DateTime? nowValue = now;
        DateTime? expires = now + lease;

        var rows = await _db.SyncStates
            .Where(s => s.Name == jobName && s.LeaseOwner == owner)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.LeaseExpiresAtUtc, expires)
                .SetProperty(s => s.HeartbeatAtUtc, nowValue), ct);

        if (rows == 0)
        {
            _log.LogWarning("[WORKER] {Job}: {Owner} perdió el candado y debe abortar", jobName, owner);
        }

        return rows == 1;
    }

    public async Task<bool> ReleaseAsync(string jobName, string owner, int itemsProcessed, int itemsFailed,
        string? error = null, CancellationToken ct = default)
    {
        DateTime? now = _time.GetUtcNow().UtcDateTime;
        var owned = _db.SyncStates.Where(s => s.Name == jobName && s.LeaseOwner == owner);

        int rows;
        if (error is null)
        {
            rows = await owned.ExecuteUpdateAsync(set => set
                .SetProperty(s => s.LeaseOwner, (string?)null)
                .SetProperty(s => s.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(s => s.LastFinishedAtUtc, now)
                .SetProperty(s => s.LastSucceededAtUtc, now)
                .SetProperty(s => s.LastItemsProcessed, itemsProcessed)
                .SetProperty(s => s.LastItemsFailed, itemsFailed)
                .SetProperty(s => s.LastError, (string?)null), ct);
        }
        else
        {
            string trimmed = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;

            rows = await owned.ExecuteUpdateAsync(set => set
                .SetProperty(s => s.LeaseOwner, (string?)null)
                .SetProperty(s => s.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(s => s.LastFinishedAtUtc, now)
                .SetProperty(s => s.LastItemsProcessed, itemsProcessed)
                .SetProperty(s => s.LastItemsFailed, itemsFailed)
                .SetProperty(s => s.LastError, trimmed), ct);
        }

        if (rows == 0)
        {
            _log.LogWarning("[WORKER] {Job}: {Owner} intentó soltar un candado que no era suyo", jobName, owner);
        }

        return rows == 1;
    }

    /// <summary>Crea la fila de la tarea si no existe. Si dos instancias compiten, una pierde y se ignora.</summary>
    private async Task EnsureRowAsync(string jobName, CancellationToken ct)
    {
        if (await _db.SyncStates.AnyAsync(s => s.Name == jobName, ct))
        {
            return;
        }

        var entity = new SyncState { Name = jobName };
        _db.SyncStates.Add(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Otra instancia la creó a la vez: nos quedamos con la suya.
            _db.Entry(entity).State = EntityState.Detached;
        }
    }
}
