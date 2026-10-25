namespace CriptoPortfolio.Application.Abstractions;

/// <summary>
/// Candado (lease) en SQL para que una tarea de sincronización no corra dos veces a la vez,
/// ni siquiera con varias instancias del backend. Los workers lo usan así:
/// TryAcquire → (trabajar + Heartbeat cada poco) → Release.
/// </summary>
public interface ISyncLockService
{
    /// <summary>Intenta tomar el candado. True si es nuestro (libre, caducado o ya nuestro).</summary>
    Task<bool> TryAcquireAsync(string jobName, string owner, TimeSpan lease, CancellationToken ct = default);

    /// <summary>Alarga el candado. False si lo perdimos (otro dueño lo tomó): hay que abortar.</summary>
    Task<bool> HeartbeatAsync(string jobName, string owner, TimeSpan lease, CancellationToken ct = default);

    /// <summary>
    /// Suelta el candado y anota el resultado. error == null significa éxito.
    /// False si no éramos el dueño (no se toca nada).
    /// </summary>
    Task<bool> ReleaseAsync(string jobName, string owner, int itemsProcessed, int itemsFailed,
        string? error = null, CancellationToken ct = default);
}
