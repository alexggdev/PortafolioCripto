using CriptoPortfolio.Application.History;

namespace CriptoPortfolio.Application.Abstractions;

public interface IHistorySyncService
{
    /// <summary>
    /// Carga el histórico diario que falte (carga inicial y días nuevos). Con candado en SQL:
    /// si otra instancia ya lo está haciendo, devuelve Executed = false.
    /// </summary>
    Task<HistorySyncResult> SyncAsync(string owner, CancellationToken ct = default);
}
