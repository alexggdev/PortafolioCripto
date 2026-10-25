using CriptoPortfolio.Domain.Entities;

namespace CriptoPortfolio.Domain.CostBasis;

/// <summary>Unidades que todavía se conservan, con lo que costaron (en USD, comisiones incluidas).</summary>
public sealed record OpenLot(DateTime AcquiredUtc, decimal Quantity, decimal CostUsd)
{
    public decimal UnitCostUsd => Quantity == 0 ? 0 : CostUsd / Quantity;
}

/// <summary>Resultado de repasar todas las transacciones de UNA moneda.</summary>
public sealed record CostBasisResult(
    decimal Quantity,
    decimal CostBasisUsd,
    decimal RealizedPnlUsd,
    IReadOnlyList<OpenLot> OpenLots)
{
    public static readonly CostBasisResult Empty = new(0m, 0m, 0m, Array.Empty<OpenLot>());

    /// <summary>Coste medio por unidad de lo que se conserva.</summary>
    public decimal AverageCostUsd => Quantity == 0 ? 0 : CostBasisUsd / Quantity;
}

/// <summary>Una venta, transferencia de salida o comisión pide más unidades de las que había en ese momento.</summary>
public sealed class InsufficientHoldingsException : Exception
{
    public InsufficientHoldingsException(Transaction transaction, decimal missing)
        : base($"La transacción {transaction.Id} ({transaction.Type}) mueve {missing} unidades más de las disponibles.")
    {
        Transaction = transaction;
        Missing = missing;
    }

    public Transaction Transaction { get; }

    /// <summary>Unidades que faltaban.</summary>
    public decimal Missing { get; }
}
