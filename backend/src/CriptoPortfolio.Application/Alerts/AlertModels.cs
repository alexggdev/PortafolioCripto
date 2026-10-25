using CriptoPortfolio.Domain.Enums;

namespace CriptoPortfolio.Application.Alerts;

public sealed record PriceAlertDto(
    long Id,
    string AssetSlug,
    string Symbol,
    string Name,
    string? ImageUrl,
    AlertDirection Direction,
    decimal TargetPriceUsd,
    // Precio actual: solo en alertas activas y si hay un precio reciente.
    decimal? CurrentPriceUsd,
    DateTime CreatedAtUtc,
    DateTime? TriggeredAtUtc,
    decimal? TriggeredPriceUsd,
    // Disparada y todavía sin ver.
    bool Unread);

public sealed record CreateAlertRequest(string? AssetSlug, AlertDirection Direction, decimal TargetPriceUsd);

public enum AlertFailure
{
    None,
    Invalid,
    NotFound,
    LimitReached
}

public sealed record AlertResult(AlertFailure Failure, PriceAlertDto? Alert, IReadOnlyList<string> Errors)
{
    public bool Succeeded => Failure == AlertFailure.None;

    public static AlertResult Ok(PriceAlertDto alert) => new(AlertFailure.None, alert, Array.Empty<string>());

    public static AlertResult Fail(AlertFailure failure, params string[] errors) => new(failure, null, errors);
}

/// <summary>Alertas de precio del usuario. Solo usa SQL y la caché en memoria de precios.</summary>
public interface IPriceAlertService
{
    /// <summary>Todas las del usuario (activas y disparadas), las más recientes primero.</summary>
    Task<IReadOnlyList<PriceAlertDto>> ListAsync(Guid userId, CancellationToken ct = default);

    Task<AlertResult> CreateAsync(Guid userId, CreateAlertRequest request, CancellationToken ct = default);

    /// <summary>false si la alerta no existe o no es del usuario.</summary>
    Task<bool> DeleteAsync(Guid userId, long id, CancellationToken ct = default);

    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Marca como vistas todas las alertas disparadas del usuario.</summary>
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}

public sealed record AlertEvaluationResult(bool Ran, int Checked, int Triggered)
{
    public static AlertEvaluationResult Skipped() => new(false, 0, 0);
}

/// <summary>Revisa las alertas activas contra los precios actuales y dispara las que se cumplen.</summary>
public interface IPriceAlertEvaluator
{
    /// <param name="owner">Identifica a esta instancia en el candado: solo una evalúa a la vez.</param>
    Task<AlertEvaluationResult> EvaluateAsync(string owner, CancellationToken ct = default);
}
