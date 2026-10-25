using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Domain.History;
using Xunit;

namespace CriptoPortfolio.Tests.Domain;

public class PortfolioHistoryCalculatorTests
{
    private const int Btc = 1;
    private const int Eth = 2;

    private static DateOnly D(int day) => new(2026, 10, day);

    private static DateTime At(int day, int hour = 12) => new(2026, 10, day, hour, 0, 0, DateTimeKind.Utc);

    private static Transaction Tx(
        long id, int asset, TransactionType type, decimal qty, decimal price, int day, decimal fee = 0m, int hour = 12) =>
        new() { Id = id, PortfolioId = Guid.Empty, AssetId = asset, Type = type, Quantity = qty, UnitPriceUsd = price, FeeUsd = fee, DateUtc = At(day, hour) };

    private static DailyPriceBook Book(int asset, params (int Day, decimal Close)[] closes)
    {
        var book = new DailyPriceBook();
        book.Add(asset, new DailyCloseSeries(closes.Select(c => new KeyValuePair<DateOnly, decimal>(D(c.Day), c.Close))));
        return book;
    }

    [Fact]
    public void A_buy_is_valued_at_each_days_close_and_counts_as_contributed_with_its_fee()
    {
        var days = PortfolioHistoryCalculator.Compute(
            new[] { Tx(1, Btc, TransactionType.Buy, 2m, 100m, 1, fee: 1m) },
            Book(Btc, (1, 100m), (2, 110m), (3, 90m)), D(1), D(3));

        Assert.Equal(new[] { 200m, 220m, 180m }, days.Select(d => d.ValueUsd));
        Assert.All(days, d => Assert.Equal(201m, d.NetContributedUsd));
        Assert.Equal(new[] { D(1), D(2), D(3) }, days.Select(d => d.Date));
    }

    [Fact]
    public void A_sell_reduces_the_holding_and_returns_money_net_of_the_fee()
    {
        var days = PortfolioHistoryCalculator.Compute(
            new[]
            {
                Tx(1, Btc, TransactionType.Buy, 2m, 100m, 1, fee: 1m),
                Tx(2, Btc, TransactionType.Sell, 1m, 120m, 3, fee: 2m)
            },
            Book(Btc, (1, 100m), (2, 110m), (3, 90m)), D(1), D(3));

        var last = days[^1];
        Assert.Equal(90m, last.ValueUsd);                // 1 unidad × 90
        Assert.Equal(201m - (120m - 2m), last.NetContributedUsd); // 83
    }

    [Fact]
    public void A_day_without_a_close_uses_the_latest_previous_close()
    {
        var days = PortfolioHistoryCalculator.Compute(
            new[] { Tx(1, Btc, TransactionType.Buy, 2m, 100m, 1) },
            Book(Btc, (1, 100m), (3, 90m)), D(1), D(3));

        Assert.Equal(200m, days[1].ValueUsd); // día 2: sigue el cierre del día 1
        Assert.Equal(180m, days[2].ValueUsd);
    }

    [Fact]
    public void Before_the_first_close_the_users_own_price_is_used()
    {
        // Compra el día 1 a 50; el histórico de la moneda empieza el día 3.
        var days = PortfolioHistoryCalculator.Compute(
            new[] { Tx(1, Btc, TransactionType.Buy, 1m, 50m, 1) },
            Book(Btc, (3, 100m)), D(1), D(3));

        Assert.Equal(new[] { 50m, 50m, 100m }, days.Select(d => d.ValueUsd));
    }

    [Fact]
    public void Without_any_price_a_holding_is_worth_zero()
    {
        var days = PortfolioHistoryCalculator.Compute(
            new[] { Tx(1, Btc, TransactionType.Airdrop, 5m, 0m, 1) },
            new DailyPriceBook(), D(1), D(2));

        Assert.All(days, d => Assert.Equal(0m, d.ValueUsd));
    }

    [Fact]
    public void Staking_and_airdrop_are_gains_not_contributions_except_their_fee()
    {
        var days = PortfolioHistoryCalculator.Compute(
            new[]
            {
                Tx(1, Btc, TransactionType.Staking, 1m, 0m, 1),
                Tx(2, Btc, TransactionType.Airdrop, 1m, 0m, 1, fee: 1m)
            },
            Book(Btc, (1, 100m)), D(1), D(1));

        var day = Assert.Single(days);
        Assert.Equal(200m, day.ValueUsd);
        Assert.Equal(1m, day.NetContributedUsd);
    }

    [Fact]
    public void Transfers_without_a_price_are_valued_at_that_days_close_so_they_are_not_gains_or_losses()
    {
        var days = PortfolioHistoryCalculator.Compute(
            new[]
            {
                Tx(1, Btc, TransactionType.TransferIn, 2m, 0m, 1),
                Tx(2, Btc, TransactionType.TransferOut, 1m, 0m, 2)
            },
            Book(Btc, (1, 100m), (2, 110m)), D(1), D(2));

        Assert.Equal(200m, days[0].ValueUsd);
        Assert.Equal(200m, days[0].NetContributedUsd);
        Assert.Equal(110m, days[1].ValueUsd);
        Assert.Equal(90m, days[1].NetContributedUsd); // 200 − 1 × 110
    }

    [Fact]
    public void A_fee_paid_in_the_coin_removes_units_without_changing_contributions()
    {
        var days = PortfolioHistoryCalculator.Compute(
            new[]
            {
                Tx(1, Btc, TransactionType.Buy, 1m, 100m, 1),
                Tx(2, Btc, TransactionType.Fee, 0.1m, 0m, 2)
            },
            Book(Btc, (1, 100m), (2, 100m)), D(1), D(2));

        Assert.Equal(90m, days[1].ValueUsd);
        Assert.Equal(100m, days[1].NetContributedUsd);
    }

    [Fact]
    public void Several_coins_add_up()
    {
        var book = Book(Btc, (1, 100m));
        book.Add(Eth, new DailyCloseSeries(new[] { new KeyValuePair<DateOnly, decimal>(D(1), 10m) }));

        var days = PortfolioHistoryCalculator.Compute(
            new[]
            {
                Tx(1, Btc, TransactionType.Buy, 1m, 100m, 1),
                Tx(2, Eth, TransactionType.Buy, 5m, 10m, 1)
            },
            book, D(1), D(1));

        Assert.Equal(150m, days[0].ValueUsd);
        Assert.Equal(150m, days[0].NetContributedUsd);
    }

    [Fact]
    public void From_only_limits_the_output_but_the_whole_history_is_replayed()
    {
        var days = PortfolioHistoryCalculator.Compute(
            new[] { Tx(1, Btc, TransactionType.Buy, 2m, 100m, 1) },
            Book(Btc, (1, 100m), (2, 110m), (3, 90m)), D(3), D(3));

        var day = Assert.Single(days);
        Assert.Equal(D(3), day.Date);
        Assert.Equal(180m, day.ValueUsd);
        Assert.Equal(200m, day.NetContributedUsd);
    }

    [Fact]
    public void The_series_starts_on_the_first_transaction_day()
    {
        var days = PortfolioHistoryCalculator.Compute(
            new[] { Tx(1, Btc, TransactionType.Buy, 1m, 100m, 5) },
            Book(Btc, (1, 100m)), D(1), D(7));

        Assert.Equal(D(5), days[0].Date);
        Assert.Equal(3, days.Count);
    }

    [Fact]
    public void No_transactions_or_an_end_before_the_first_one_gives_an_empty_series()
    {
        Assert.Empty(PortfolioHistoryCalculator.Compute(Array.Empty<Transaction>(), new DailyPriceBook(), D(1), D(5)));
        Assert.Empty(PortfolioHistoryCalculator.Compute(
            new[] { Tx(1, Btc, TransactionType.Buy, 1m, 100m, 5) }, new DailyPriceBook(), D(1), D(4)));
    }

    [Fact]
    public void A_buy_and_a_sell_on_the_same_day_only_show_their_end_of_day_result()
    {
        var days = PortfolioHistoryCalculator.Compute(
            new[]
            {
                Tx(1, Btc, TransactionType.Buy, 1m, 100m, 1, hour: 9),
                Tx(2, Btc, TransactionType.Sell, 1m, 110m, 1, hour: 18)
            },
            Book(Btc, (1, 105m)), D(1), D(1));

        Assert.Equal(0m, days[0].ValueUsd);
        Assert.Equal(-10m, days[0].NetContributedUsd);
    }

    [Fact]
    public void The_close_series_ignores_non_positive_prices_and_the_overlay_only_changes_its_day()
    {
        var series = new DailyCloseSeries(new[]
        {
            new KeyValuePair<DateOnly, decimal>(D(1), 100m),
            new KeyValuePair<DateOnly, decimal>(D(2), 0m),
            new KeyValuePair<DateOnly, decimal>(D(3), -5m)
        });
        var book = new DailyPriceBook();
        book.Add(Btc, series);

        Assert.Equal(1, series.Count);
        Assert.Equal(100m, book.PriceOn(Btc, D(3)));
        Assert.Null(book.PriceOn(Btc, new DateOnly(2026, 9, 30)));

        var overlay = new OverlayPrices(book, D(3), new Dictionary<int, decimal> { [Btc] = 130m });
        Assert.Equal(130m, overlay.PriceOn(Btc, D(3)));
        Assert.Equal(100m, overlay.PriceOn(Btc, D(2)));
    }
}
