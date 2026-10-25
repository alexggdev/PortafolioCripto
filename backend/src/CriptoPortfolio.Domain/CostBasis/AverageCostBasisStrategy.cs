using CriptoPortfolio.Domain.Entities;

namespace CriptoPortfolio.Domain.CostBasis;

/// <summary>Media ponderada: todo lo que se conserva tiene el mismo coste medio por unidad.</summary>
public sealed class AverageCostBasisStrategy : ICostBasisStrategy
{
    public string Name => "Average";

    public CostBasisResult Compute(IEnumerable<Transaction> transactions)
    {
        var quantity = 0m;
        var cost = 0m;
        var realized = 0m;
        DateTime? firstAcquired = null;

        foreach (var t in TransactionRules.Sort(transactions))
        {
            TransactionRules.EnsurePositive(t);

            if (TransactionRules.IsAcquisition(t.Type))
            {
                firstAcquired ??= t.DateUtc;
                quantity += t.Quantity;
                cost += TransactionRules.AcquisitionCost(t);
                continue;
            }

            if (t.Quantity > quantity)
            {
                throw new InsufficientHoldingsException(t, t.Quantity - quantity);
            }

            var consumedCost = t.Quantity == quantity ? cost : cost * t.Quantity / quantity;
            quantity -= t.Quantity;
            cost = quantity == 0 ? 0m : cost - consumedCost;
            if (quantity == 0)
            {
                firstAcquired = null; // al vender todo, la siguiente compra empieza una posición nueva
            }

            realized += TransactionRules.RealizedPnl(t, consumedCost);
        }

        if (quantity == 0)
        {
            return new CostBasisResult(0m, 0m, realized, Array.Empty<OpenLot>());
        }

        // Un único lote con el coste medio (la fecha es la de la primera compra).
        return new CostBasisResult(quantity, cost, realized, new[] { new OpenLot(firstAcquired!.Value, quantity, cost) });
    }
}
