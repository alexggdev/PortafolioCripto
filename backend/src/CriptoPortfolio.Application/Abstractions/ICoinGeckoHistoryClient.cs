using CriptoPortfolio.Application.History;

namespace CriptoPortfolio.Application.Abstractions;

public interface ICoinGeckoHistoryClient
{
    /// <summary>
    /// Cierres diarios de los últimos 'days' días, de más antiguo a más reciente. Solo trae el cierre
    /// (y el volumen); el día en curso no se incluye. El plan Demo llega hasta 365 días.
    /// </summary>
    Task<IReadOnlyList<DailyPrice>> GetDailyPricesAsync(string coinGeckoId, int days, CancellationToken ct = default);
}
