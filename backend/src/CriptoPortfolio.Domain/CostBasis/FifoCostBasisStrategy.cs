using CriptoPortfolio.Domain.Entities;

namespace CriptoPortfolio.Domain.CostBasis;

/// <summary>Primero en entrar, primero en salir: lo que sale se lleva el coste de las unidades más antiguas.</summary>
public sealed class FifoCostBasisStrategy : ICostBasisStrategy
{
    private sealed class Lot
    {
        public Lot(DateTime acquiredUtc, decimal quantity, decimal cost)
        {
            AcquiredUtc = acquiredUtc;
            Quantity = quantity;
            Cost = cost;
        }

        public DateTime AcquiredUtc { get; }
        public decimal Quantity { get; set; }
        public decimal Cost { get; set; }
    }

    public string Name => "Fifo";

    public CostBasisResult Compute(IEnumerable<Transaction> transactions)
    {
        var lots = new Queue<Lot>();
        var realized = 0m;

        foreach (var t in TransactionRules.Sort(transactions))
        {
            TransactionRules.EnsurePositive(t);

            if (TransactionRules.IsAcquisition(t.Type))
            {
                lots.Enqueue(new Lot(t.DateUtc, t.Quantity, TransactionRules.AcquisitionCost(t)));
                continue;
            }

            var remaining = t.Quantity;
            var consumedCost = 0m;

            while (remaining > 0)
            {
                if (lots.Count == 0)
                {
                    throw new InsufficientHoldingsException(t, remaining);
                }

                var lot = lots.Peek();
                if (lot.Quantity <= remaining)
                {
                    // Lote entero: se lleva todo su coste, sin redondeos.
                    consumedCost += lot.Cost;
                    remaining -= lot.Quantity;
                    lots.Dequeue();
                }
                else
                {
                    var part = lot.Cost * remaining / lot.Quantity;
                    consumedCost += part;
                    lot.Cost -= part;
                    lot.Quantity -= remaining;
                    remaining = 0;
                }
            }

            realized += TransactionRules.RealizedPnl(t, consumedCost);
        }

        if (lots.Count == 0)
        {
            return new CostBasisResult(0m, 0m, realized, Array.Empty<OpenLot>());
        }

        var open = lots.Select(l => new OpenLot(l.AcquiredUtc, l.Quantity, l.Cost)).ToList();
        return new CostBasisResult(open.Sum(l => l.Quantity), open.Sum(l => l.CostUsd), realized, open);
    }
}
