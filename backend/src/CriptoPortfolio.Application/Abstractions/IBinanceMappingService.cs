using CriptoPortfolio.Application.Prices;

namespace CriptoPortfolio.Application.Abstractions;

public interface IBinanceMappingService
{
    /// <summary>
    /// Asigna a cada moneda del catálogo su par "XXXUSDT" de Binance (si existe). Toma el candado en SQL.
    /// Los fallos de Binance no lanzan excepción: quedan en el resultado y en SyncStates.
    /// </summary>
    Task<BinanceMappingResult> SyncAsync(string owner, CancellationToken ct = default);
}
