namespace CriptoPortfolio.Application.Prices;

/// <summary>Resultado de una pasada del mapa de símbolos de Binance.</summary>
/// <param name="Executed">False si se omitió porque otra instancia tenía el candado.</param>
/// <param name="Assigned">Monedas a las que se asignó (o cambió) el par.</param>
/// <param name="Released">Monedas a las que se les quitó el par.</param>
/// <param name="Rejected">Asignaciones descartadas porque el precio de Binance no cuadra con el de CoinGecko.</param>
public sealed record BinanceMappingResult(bool Executed, int Assigned, int Released, int Rejected, string? Error)
{
    public bool Succeeded => Executed && Error is null;
}
