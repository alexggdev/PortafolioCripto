using CriptoPortfolio.Domain.CostBasis;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using Xunit;

namespace CriptoPortfolio.Tests.Domain;

public class CostBasisTests
{
    private static readonly DateTime Day1 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Transaction Tx(long id, TransactionType type, decimal qty, decimal price, int day, decimal fee = 0m) => new()
    {
        Id = id,
        Type = type,
        Quantity = qty,
        UnitPriceUsd = price,
        FeeUsd = fee,
        DateUtc = Day1.AddDays(day)
    };

    public static IEnumerable<object[]> Strategies() => new[]
    {
        new object[] { new FifoCostBasisStrategy() },
        new object[] { new AverageCostBasisStrategy() }
    };

    // ---------------------------------------------------------- comunes

    [Theory, MemberData(nameof(Strategies))]
    public void No_transactions_means_nothing_held(ICostBasisStrategy strategy)
    {
        var r = strategy.Compute(Array.Empty<Transaction>());

        Assert.Equal(0m, r.Quantity);
        Assert.Equal(0m, r.CostBasisUsd);
        Assert.Equal(0m, r.RealizedPnlUsd);
        Assert.Empty(r.OpenLots);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void A_buy_costs_quantity_times_price_plus_fee(ICostBasisStrategy strategy)
    {
        var r = strategy.Compute(new[] { Tx(1, TransactionType.Buy, 2m, 100m, 0, fee: 5m) });

        Assert.Equal(2m, r.Quantity);
        Assert.Equal(205m, r.CostBasisUsd);
        Assert.Equal(102.5m, r.AverageCostUsd);
        Assert.Equal(0m, r.RealizedPnlUsd);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void A_sale_realizes_proceeds_minus_fee_minus_cost(ICostBasisStrategy strategy)
    {
        var r = strategy.Compute(new[]
        {
            Tx(1, TransactionType.Buy, 1m, 100m, 0, fee: 5m),   // coste 105
            Tx(2, TransactionType.Sell, 1m, 150m, 1, fee: 3m)   // 150 − 3 − 105
        });

        Assert.Equal(42m, r.RealizedPnlUsd);
        Assert.Equal(0m, r.Quantity);
        Assert.Equal(0m, r.CostBasisUsd);
        Assert.Empty(r.OpenLots);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void Staking_and_airdrops_come_in_at_zero_cost_except_their_fee(ICostBasisStrategy strategy)
    {
        var r = strategy.Compute(new[]
        {
            Tx(1, TransactionType.Staking, 2m, 50m, 0),                // el precio se ignora
            Tx(2, TransactionType.Airdrop, 1m, 999m, 0, fee: 1m)       // solo cuenta la comisión
        });

        Assert.Equal(3m, r.Quantity);
        Assert.Equal(1m, r.CostBasisUsd);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void Selling_staked_coins_is_all_profit(ICostBasisStrategy strategy)
    {
        var r = strategy.Compute(new[]
        {
            Tx(1, TransactionType.Staking, 2m, 0m, 0),
            Tx(2, TransactionType.Sell, 1m, 10m, 1)
        });

        Assert.Equal(10m, r.RealizedPnlUsd);
        Assert.Equal(1m, r.Quantity);
        Assert.Equal(0m, r.CostBasisUsd);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void A_transfer_in_enters_at_its_declared_price(ICostBasisStrategy strategy)
    {
        var r = strategy.Compute(new[] { Tx(1, TransactionType.TransferIn, 2m, 50m, 0) });

        Assert.Equal(100m, r.CostBasisUsd);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void A_transfer_out_keeps_the_cost_of_what_stays_and_only_the_fee_is_a_loss(ICostBasisStrategy strategy)
    {
        var r = strategy.Compute(new[]
        {
            Tx(1, TransactionType.Buy, 2m, 100m, 0),
            Tx(2, TransactionType.TransferOut, 1m, 0m, 1, fee: 2m)
        });

        Assert.Equal(1m, r.Quantity);
        Assert.Equal(100m, r.CostBasisUsd);
        Assert.Equal(-2m, r.RealizedPnlUsd);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void A_fee_paid_in_the_coin_itself_burns_its_cost(ICostBasisStrategy strategy)
    {
        var r = strategy.Compute(new[]
        {
            Tx(1, TransactionType.Buy, 2m, 100m, 0),
            Tx(2, TransactionType.Fee, 0.5m, 0m, 1, fee: 1m)   // 0,5 × 100 = 50 de coste + 1 de comisión
        });

        Assert.Equal(1.5m, r.Quantity);
        Assert.Equal(150m, r.CostBasisUsd);
        Assert.Equal(-51m, r.RealizedPnlUsd);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void Selling_more_than_held_throws_and_reports_how_much_was_missing(ICostBasisStrategy strategy)
    {
        var ex = Assert.Throws<InsufficientHoldingsException>(() => strategy.Compute(new[]
        {
            Tx(1, TransactionType.Buy, 1m, 100m, 0),
            Tx(2, TransactionType.Sell, 2.5m, 100m, 1)
        }));

        Assert.Equal(1.5m, ex.Missing);
        Assert.Equal(2, ex.Transaction.Id);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void Selling_before_buying_throws(ICostBasisStrategy strategy)
    {
        Assert.Throws<InsufficientHoldingsException>(() => strategy.Compute(new[]
        {
            Tx(1, TransactionType.Sell, 1m, 100m, 0),
            Tx(2, TransactionType.Buy, 1m, 100m, 1)
        }));
    }

    [Theory, MemberData(nameof(Strategies))]
    public void Input_order_does_not_matter_only_dates(ICostBasisStrategy strategy)
    {
        var r = strategy.Compute(new[]
        {
            Tx(2, TransactionType.Sell, 1m, 150m, 1),
            Tx(1, TransactionType.Buy, 1m, 100m, 0)
        });

        Assert.Equal(50m, r.RealizedPnlUsd);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void At_the_same_instant_entries_come_before_exits(ICostBasisStrategy strategy)
    {
        var r = strategy.Compute(new[]
        {
            Tx(1, TransactionType.Sell, 1m, 150m, 0),
            Tx(2, TransactionType.Buy, 1m, 100m, 0)
        });

        Assert.Equal(50m, r.RealizedPnlUsd);
        Assert.Equal(0m, r.Quantity);
    }

    [Theory, MemberData(nameof(Strategies))]
    public void Non_positive_quantities_are_rejected(ICostBasisStrategy strategy)
    {
        Assert.Throws<ArgumentException>(() => strategy.Compute(new[] { Tx(1, TransactionType.Buy, 0m, 100m, 0) }));
        Assert.Throws<ArgumentException>(() => strategy.Compute(new[] { Tx(1, TransactionType.Buy, -1m, 100m, 0) }));
    }

    // ------------------------------------------------------------- FIFO

    [Fact]
    public void Fifo_sells_the_oldest_units_first()
    {
        var r = new FifoCostBasisStrategy().Compute(new[]
        {
            Tx(1, TransactionType.Buy, 1m, 100m, 0),
            Tx(2, TransactionType.Buy, 1m, 200m, 1),
            Tx(3, TransactionType.Sell, 1.5m, 300m, 2)   // lote 1 entero (100) + mitad del lote 2 (100)
        });

        Assert.Equal(250m, r.RealizedPnlUsd);            // 450 − 200
        Assert.Equal(0.5m, r.Quantity);
        Assert.Equal(100m, r.CostBasisUsd);
        Assert.Equal(200m, r.AverageCostUsd);

        var lot = Assert.Single(r.OpenLots);
        Assert.Equal(Day1.AddDays(1), lot.AcquiredUtc);
    }

    [Fact]
    public void Fifo_keeps_one_open_lot_per_purchase()
    {
        var r = new FifoCostBasisStrategy().Compute(new[]
        {
            Tx(1, TransactionType.Buy, 1m, 100m, 0),
            Tx(2, TransactionType.Buy, 2m, 200m, 1)
        });

        Assert.Equal(2, r.OpenLots.Count);
        Assert.Equal(3m, r.Quantity);
        Assert.Equal(500m, r.CostBasisUsd);
    }

    [Fact]
    public void Fifo_loses_no_cost_to_rounding_when_a_lot_is_sold_in_thirds()
    {
        var r = new FifoCostBasisStrategy().Compute(new[]
        {
            Tx(1, TransactionType.Buy, 3m, 1m, 0),
            Tx(2, TransactionType.Sell, 1m, 2m, 1),
            Tx(3, TransactionType.Sell, 1m, 2m, 2),
            Tx(4, TransactionType.Sell, 1m, 2m, 3)
        });

        Assert.Equal(0m, r.Quantity);
        Assert.Equal(0m, r.CostBasisUsd);
        Assert.Equal(3m, r.RealizedPnlUsd);   // 6 de ingresos − 3 de coste
    }

    // ------------------------------------------------------------ media

    [Fact]
    public void Average_spreads_the_cost_over_all_units()
    {
        var r = new AverageCostBasisStrategy().Compute(new[]
        {
            Tx(1, TransactionType.Buy, 1m, 100m, 0),
            Tx(2, TransactionType.Buy, 1m, 200m, 1),
            Tx(3, TransactionType.Sell, 1.5m, 300m, 2)   // coste medio 150 → se llevan 225
        });

        Assert.Equal(225m, r.RealizedPnlUsd);            // 450 − 225
        Assert.Equal(0.5m, r.Quantity);
        Assert.Equal(75m, r.CostBasisUsd);

        var lot = Assert.Single(r.OpenLots);
        Assert.Equal(Day1, lot.AcquiredUtc);             // fecha de la primera compra
    }

    [Fact]
    public void Average_starts_again_after_selling_everything()
    {
        var r = new AverageCostBasisStrategy().Compute(new[]
        {
            Tx(1, TransactionType.Buy, 1m, 100m, 0),
            Tx(2, TransactionType.Sell, 1m, 150m, 1),    // +50
            Tx(3, TransactionType.Buy, 1m, 200m, 2),
            Tx(4, TransactionType.Sell, 0.5m, 300m, 3)   // 150 − 100 = +50
        });

        Assert.Equal(100m, r.RealizedPnlUsd);
        Assert.Equal(0.5m, r.Quantity);
        Assert.Equal(100m, r.CostBasisUsd);

        var lot = Assert.Single(r.OpenLots);
        Assert.Equal(Day1.AddDays(2), lot.AcquiredUtc);   // la posición nueva empieza en la segunda compra
    }
}
