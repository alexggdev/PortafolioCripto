using CriptoPortfolio.Application.History;

namespace CriptoPortfolio.Application.Abstractions;

public interface IBinanceHistoryClient
{
    /// <summary>
    /// Velas diarias CERRADAS del par (p. ej. "BTCUSDT") desde la fecha indicada, de la más antigua a la más reciente.
    /// El día en curso no se incluye. Pagina por dentro (Binance da 1000 velas por llamada).
    /// </summary>
    Task<IReadOnlyList<DailyPrice>> GetDailyPricesAsync(string symbol, DateOnly from, CancellationToken ct = default);
}
