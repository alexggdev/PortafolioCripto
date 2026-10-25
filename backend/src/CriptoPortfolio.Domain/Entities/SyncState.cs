namespace CriptoPortfolio.Domain.Entities;

/// <summary>
/// Candado (lease) de una tarea de sincronización. Solo un proceso puede tenerlo a la vez;
/// si el dueño deja de enviar latidos, el candado caduca y otro proceso lo toma.
/// </summary>
public class SyncState
{
    public string Name { get; set; } = "";

    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime? HeartbeatAtUtc { get; set; }

    public DateTime? LastStartedAtUtc { get; set; }
    public DateTime? LastFinishedAtUtc { get; set; }
    public DateTime? LastSucceededAtUtc { get; set; }
    public int LastItemsProcessed { get; set; }
    public int LastItemsFailed { get; set; }
    public string? LastError { get; set; }
}
