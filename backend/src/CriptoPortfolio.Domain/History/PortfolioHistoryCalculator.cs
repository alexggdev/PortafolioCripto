using CriptoPortfolio.Domain.CostBasis;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;

namespace CriptoPortfolio.Domain.History;

/// <summary>Valor del portafolio al final de un día (UTC) y dinero neto que el usuario ha puesto hasta ese día.</summary>
public sealed record HistoryDay(DateOnly Date, decimal ValueUsd, decimal NetContributedUsd);

/// <summary>
/// Reconstruye día a día el portafolio a partir de las transacciones. Código puro: sin base de datos ni reloj.
///
/// Valor del día = suma de (cantidad que tenía al final del día × cierre de ese día), por moneda.
/// Si la moneda aún no tiene cierre ese día, se usa el último cierre anterior; si no hay ninguno
/// (día anterior a su histórico), el último precio unitario que el usuario anotó para esa moneda (0 si no anotó ninguno).
///
/// Aportado neto = dinero que entró menos dinero que salió, en USD:
///   Compra y transferencia recibida: +(cantidad × precio + comisión). Sin precio, la transferencia se valora al cierre del día.
///   Venta: −(cantidad × precio − comisión).
///   Transferencia enviada: −(cantidad × precio de ese día) + comisión (sale valor, no es pérdida).
///   Staking y airdrop: solo la comisión (lo recibido es ganancia, no aportación).
///   Comisión pagada en la moneda: +comisión (las unidades perdidas sí son pérdida).
/// </summary>
public static class PortfolioHistoryCalculator
{
    /// <summary>
    /// Días entre <paramref name="from"/> y <paramref name="to"/> (ambos incluidos). Siempre se reproduce todo el historial
    /// desde la primera transacción; <paramref name="from"/> solo limita qué días se devuelven.
    /// </summary>
    public static IReadOnlyList<HistoryDay> Compute(
        IEnumerable<Transaction> transactions, IDailyPrices prices, DateOnly from, DateOnly to)
    {
        var sorted = TransactionRules.Sort(transactions);
        var result = new List<HistoryDay>();
        if (sorted.Count == 0)
        {
            return result;
        }

        var holdings = new Dictionary<int, decimal>();
        var lastOwnPrice = new Dictionary<int, decimal>();
        var contributed = 0m;
        var next = 0;

        for (var day = DateOnly.FromDateTime(sorted[0].DateUtc); day <= to; day = day.AddDays(1))
        {
            while (next < sorted.Count && DateOnly.FromDateTime(sorted[next].DateUtc) <= day)
            {
                contributed += Apply(sorted[next], prices, holdings, lastOwnPrice);
                next++;
            }

            if (day < from)
            {
                continue;
            }

            var value = 0m;
            foreach (var (assetId, quantity) in holdings)
            {
                if (quantity <= 0m)
                {
                    continue;
                }

                var price = prices.PriceOn(assetId, day) ?? lastOwnPrice.GetValueOrDefault(assetId);
                value += quantity * price;
            }

            result.Add(new HistoryDay(day, decimal.Round(value, 8), decimal.Round(contributed, 8)));
        }

        return result;
    }

    /// <summary>Aplica la transacción a las cantidades y devuelve cuánto cambia lo aportado.</summary>
    private static decimal Apply(
        Transaction t, IDailyPrices prices, Dictionary<int, decimal> holdings, Dictionary<int, decimal> lastOwnPrice)
    {
        var q = t.Quantity;
        var p = t.UnitPriceUsd;
        var fee = t.FeeUsd;
        var held = holdings.GetValueOrDefault(t.AssetId);

        // Precio para valorar una transferencia que el usuario no valoró.
        decimal Reference() =>
            p > 0m
                ? p
                : prices.PriceOn(t.AssetId, DateOnly.FromDateTime(t.DateUtc)) ?? lastOwnPrice.GetValueOrDefault(t.AssetId);

        decimal flow;
        switch (t.Type)
        {
            case TransactionType.Buy:
                holdings[t.AssetId] = held + q;
                flow = q * p + fee;
                break;
            case TransactionType.Sell:
                holdings[t.AssetId] = Math.Max(0m, held - q);
                flow = -(q * p - fee);
                break;
            case TransactionType.TransferIn:
                flow = q * Reference() + fee;
                holdings[t.AssetId] = held + q;
                break;
            case TransactionType.TransferOut:
                flow = -(q * Reference()) + fee;
                holdings[t.AssetId] = Math.Max(0m, held - q);
                break;
            case TransactionType.Staking:
            case TransactionType.Airdrop:
                holdings[t.AssetId] = held + q;
                flow = fee;
                break;
            case TransactionType.Fee:
                holdings[t.AssetId] = Math.Max(0m, held - q);
                flow = fee;
                break;
            default:
                flow = 0m;
                break;
        }

        if (p > 0m)
        {
            lastOwnPrice[t.AssetId] = p;
        }

        return flow;
    }
}
