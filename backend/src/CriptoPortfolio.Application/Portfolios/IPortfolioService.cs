using CriptoPortfolio.Application.Catalog;

namespace CriptoPortfolio.Application.Portfolios;

/// <summary>Portafolio del usuario. Todas las operaciones se limitan a los datos del usuario indicado.</summary>
public interface IPortfolioService
{
    /// <summary>Con portfolioId, ese portafolio (si es del usuario); sin él, el primero.</summary>
    Task<PortfolioSummary> GetSummaryAsync(Guid userId, Guid? portfolioId = null, CancellationToken ct = default);

    /// <summary>Todos los portafolios del usuario sumados (para la vista "Resumen").</summary>
    Task<PortfolioOverview> GetOverviewAsync(Guid userId, CancellationToken ct = default);

    Task<PagedResult<TransactionDto>> ListTransactionsAsync(Guid userId, TransactionListQuery query, CancellationToken ct = default);

    /// <summary>Todas las transacciones de un portafolio, de la más antigua a la más reciente (para exportar).</summary>
    Task<IReadOnlyList<TransactionDto>> ExportTransactionsAsync(Guid userId, Guid? portfolioId = null, CancellationToken ct = default);

    Task<TransactionResult> AddTransactionAsync(Guid userId, TransactionRequest request, CancellationToken ct = default);

    Task<TransactionResult> UpdateTransactionAsync(Guid userId, long id, TransactionRequest request, CancellationToken ct = default);

    Task<TransactionResult> DeleteTransactionAsync(Guid userId, long id, CancellationToken ct = default);

    /// <summary>Quita una moneda de un portafolio: borra todas sus transacciones (también su ganancia realizada).</summary>
    Task<TransactionResult> RemoveAssetAsync(Guid userId, string assetSlug, Guid? portfolioId, CancellationToken ct = default);

    /// <summary>Mueve todas las transacciones de una moneda a otro portafolio del mismo usuario.</summary>
    Task<TransactionResult> MoveAssetAsync(
        Guid userId, string assetSlug, Guid fromPortfolioId, Guid toPortfolioId, CancellationToken ct = default);
}
