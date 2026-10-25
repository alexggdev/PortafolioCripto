using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;

namespace CriptoPortfolio.Domain.CostBasis;

/// <summary>
/// Reglas comunes a todas las estrategias:
/// - Buy, TransferIn: entran a su precio (más comisión).
/// - Staking, Airdrop: entran a coste cero (la comisión, si hay, sí cuenta).
/// - Sell: sale al precio de venta menos comisión; la ganancia es eso menos el coste de lo vendido.
/// - TransferOut: las unidades salen del portafolio con su coste, sin ganancia ni pérdida (solo cuenta la comisión).
/// - Fee: comisión pagada con la propia moneda; las unidades se pierden y su coste es pérdida.
/// </summary>
public static class TransactionRules
{
    public static bool IsAcquisition(TransactionType type) =>
        type is TransactionType.Buy or TransactionType.TransferIn or TransactionType.Staking or TransactionType.Airdrop;

    public static bool IsDisposal(TransactionType type) =>
        type is TransactionType.Sell or TransactionType.TransferOut or TransactionType.Fee;

    /// <summary>Por fecha; a la misma hora las entradas van antes que las salidas; el Id desempata.</summary>
    public static IReadOnlyList<Transaction> Sort(IEnumerable<Transaction> transactions) =>
        transactions
            .OrderBy(t => t.DateUtc)
            .ThenBy(t => IsDisposal(t.Type) ? 1 : 0)
            .ThenBy(t => t.Id)
            .ToList();

    /// <summary>Coste total de una entrada, en USD.</summary>
    public static decimal AcquisitionCost(Transaction t) =>
        t.Type is TransactionType.Staking or TransactionType.Airdrop
            ? t.FeeUsd
            : t.Quantity * t.UnitPriceUsd + t.FeeUsd;

    /// <summary>Ganancia realizada por una salida, dado el coste de las unidades que salen.</summary>
    public static decimal RealizedPnl(Transaction t, decimal consumedCost) => t.Type switch
    {
        TransactionType.Sell => t.Quantity * t.UnitPriceUsd - t.FeeUsd - consumedCost,
        TransactionType.TransferOut => -t.FeeUsd,
        TransactionType.Fee => -consumedCost - t.FeeUsd,
        _ => 0m
    };

    public static void EnsurePositive(Transaction t)
    {
        if (t.Quantity <= 0)
        {
            throw new ArgumentException($"La transacción {t.Id} tiene una cantidad no positiva.", nameof(t));
        }
    }
}
