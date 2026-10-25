using CriptoPortfolio.Application.Catalog;

namespace CriptoPortfolio.Application.Portfolios;

/// <summary>Un día de la gráfica: valor del portafolio y dinero neto aportado hasta ese día (USD).</summary>
public sealed record PortfolioHistoryPoint(DateOnly Date, decimal ValueUsd, decimal NetContributedUsd);

/// <summary>
/// Serie para la gráfica del portafolio: un punto por día (cierre UTC) y, al final, el valor de ahora con los precios actuales.
/// </summary>
/// <param name="FirstTransactionDate">Día de la primera transacción; null si no hay ninguna.</param>
/// <param name="PeriodProfitUsd">Ganancia del periodo mostrado: cambio de valor menos el dinero que entró o salió en ese tiempo.</param>
/// <param name="IncludesCurrentValue">True si el último punto es el valor de ahora y no un cierre.</param>
public sealed record PortfolioHistory(
    Guid? PortfolioId,
    string Range,
    DateOnly? FirstTransactionDate,
    IReadOnlyList<PortfolioHistoryPoint> Points,
    decimal? PeriodProfitUsd,
    bool IncludesCurrentValue);

public sealed record PortfolioSnapshotResult(bool Ran, int Portfolios, int Rebuilt, int Failed)
{
    public static PortfolioSnapshotResult Skipped() => new(false, 0, 0, 0);
}

public interface IPortfolioHistoryService
{
    Task<PortfolioHistory> GetHistoryAsync(Guid userId, ChartRange range, Guid? portfolioId = null, CancellationToken ct = default);

    /// <summary>Para el worker: completa las fotos diarias que falten de todos los portafolios.</summary>
    Task<PortfolioSnapshotResult> SnapshotAllAsync(string owner, CancellationToken ct = default);
}

/// <summary>
/// Al cambiar una transacción hay que descartar las fotos diarias desde su fecha. Marca el borrado en el contexto
/// SIN guardar: se guarda junto con la transacción, en el mismo SaveChanges, para que ambas cosas ocurran o ninguna.
/// </summary>
public interface IPortfolioHistoryInvalidator
{
    Task StageInvalidationAsync(Guid portfolioId, DateTime fromUtc, CancellationToken ct = default);
}
