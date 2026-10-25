namespace CriptoPortfolio.Application.Abstractions;

/// <summary>Datos de mercado públicos de Binance. Solo los usan los workers.</summary>
public interface IBinanceMarketClient
{
    /// <summary>Precio actual de todos los pares contra USDT. Clave: símbolo del par, p. ej. "BTCUSDT".</summary>
    Task<IReadOnlyDictionary<string, decimal>> GetUsdtPricesAsync(CancellationToken ct = default);
}
