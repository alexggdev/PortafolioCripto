using CriptoPortfolio.Application.Fx;

namespace CriptoPortfolio.Application.Abstractions;

public interface IFxRefreshService
{
    /// <summary>Pide los tipos de cambio, los valida y, si son buenos, sustituye los de la caché. No lanza excepciones.</summary>
    Task<FxRefreshResult> RefreshAsync(CancellationToken ct = default);
}
