using CriptoPortfolio.Application.Fx;

namespace CriptoPortfolio.Application.Abstractions;

public interface IFxRatesClient
{
    /// <summary>Tipos de cambio respecto al USD (unidades de cada moneda por 1 USD).</summary>
    Task<FxFetch> GetUsdRatesAsync(CancellationToken ct = default);
}
