using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Application.Prices;
using CriptoPortfolio.Domain.CostBasis;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Infrastructure.Identity;
using CriptoPortfolio.Infrastructure.Portfolios;
using CriptoPortfolio.Infrastructure.Prices;
using CriptoPortfolio.Tests.Infrastructure.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Portfolios;

public class PortfolioServiceTests : IDisposable
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly LivePriceCache _cache = new();
    private PortfolioOptions _options = new();

    public PortfolioServiceTests()
    {
        // Portfolio.UserId tiene clave foránea a la tabla de usuarios: hay que crear a Alice y a Bob.
        using var context = _db.CreateContext();
        foreach (var (id, name) in new[] { (Alice, "alice"), (Bob, "bob") })
        {
            context.Users.Add(new ApplicationUser
            {
                Id = id,
                UserName = name,
                NormalizedUserName = name.ToUpperInvariant(),
                Email = $"{name}@example.test",
                NormalizedEmail = $"{name.ToUpperInvariant()}@EXAMPLE.TEST",
                CreatedAtUtc = Now
            });
        }

        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private PortfolioService Service()
    {
        var context = _db.CreateContext();
        return new PortfolioService(
            context, _cache, new FifoCostBasisStrategy(), _time,
            Options.Create(new LivePriceOptions { StaleSeconds = 60 }),
            Options.Create(_options),
            NullLogger<PortfolioService>.Instance);
    }

    private async Task<int> AddAssetAsync(string slug, string symbol, decimal? price, decimal? change24h = null)
    {
        await using var context = _db.CreateContext();
        var asset = new Asset
        {
            Slug = slug,
            Symbol = symbol,
            Name = slug,
            CoinGeckoId = slug,
            IsActive = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        };
        if (price is not null)
        {
            asset.Price = new AssetPrice
            {
                PriceUsd = price.Value,
                Change24hPct = change24h,
                Source = DataSource.CoinGecko,
                UpdatedAtUtc = Now.AddMinutes(-5)
            };
        }

        context.Assets.Add(asset);
        await context.SaveChangesAsync();
        return asset.Id;
    }

    private TransactionRequest Req(string slug, TransactionType type, decimal qty, decimal? price, int daysAgo, decimal? fee = null) =>
        new(slug, type, qty, price, fee, Now.AddDays(-daysAgo), null);

    private async Task<TransactionDto> AddOkAsync(Guid user, TransactionRequest request)
    {
        var result = await Service().AddTransactionAsync(user, request);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Transaction!;
    }

    // -------------------------------------------------------------- resumen

    [Fact]
    public async Task A_user_without_transactions_gets_an_empty_summary()
    {
        var summary = await Service().GetSummaryAsync(Alice);

        Assert.Null(summary.PortfolioId);
        Assert.Equal("Mi portafolio", summary.Name);
        Assert.Empty(summary.Positions);
        Assert.Equal(0m, summary.ValueUsd);
        Assert.Null(summary.UnrealizedPnlPct);
    }

    [Fact]
    public async Task The_first_transaction_creates_the_portfolio_and_values_the_position_at_the_delayed_price()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);

        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 2m, 50m, 10, fee: 2m));
        var summary = await Service().GetSummaryAsync(Alice);

        Assert.NotNull(summary.PortfolioId);
        var p = Assert.Single(summary.Positions);
        Assert.Equal(2m, p.Quantity);
        Assert.Equal(102m, p.CostBasisUsd);
        Assert.Equal(51m, p.AverageCostUsd);
        Assert.Equal(100m, p.PriceUsd);
        Assert.Equal("delayed", p.PriceSource);
        Assert.Equal(200m, p.ValueUsd);
        Assert.Equal(98m, p.UnrealizedPnlUsd);
        Assert.Equal(98m / 102m * 100m, p.UnrealizedPnlPct!.Value, 6);
        Assert.Equal(100m, p.AllocationPct);

        Assert.Equal(200m, summary.ValueUsd);
        Assert.Equal(98m, summary.UnrealizedPnlUsd);
        Assert.Equal("Fifo", summary.CostBasisMethod);
    }

    [Fact]
    public async Task A_fresh_live_price_wins_over_the_delayed_one_and_a_stale_one_does_not()
    {
        var id = await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 2m, 50m, 10));

        _cache.SetRange(new[] { new KeyValuePair<int, LivePrice>(id, new LivePrice(120m, Now.AddSeconds(-5))) });
        var live = (await Service().GetSummaryAsync(Alice)).Positions.Single();

        Assert.Equal("live", live.PriceSource);
        Assert.Equal(240m, live.ValueUsd);

        _cache.SetRange(new[] { new KeyValuePair<int, LivePrice>(id, new LivePrice(120m, Now.AddMinutes(-10))) });
        var stale = (await Service().GetSummaryAsync(Alice)).Positions.Single();

        Assert.Equal("delayed", stale.PriceSource);
        Assert.Equal(200m, stale.ValueUsd);
    }

    [Fact]
    public async Task A_closed_position_disappears_but_its_realized_gain_stays_in_the_totals()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 10));
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Sell, 1m, 80m, 5));

        var summary = await Service().GetSummaryAsync(Alice);

        Assert.Empty(summary.Positions);
        Assert.Equal(30m, summary.RealizedPnlUsd);
        Assert.Equal(30m, summary.TotalPnlUsd);
        Assert.Equal(0m, summary.ValueUsd);
    }

    [Fact]
    public async Task Allocation_is_each_positions_share_of_the_priced_value()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddAssetAsync("ethereum", "ETH", 50m);
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 90m, 3));    // valor 100
        await AddOkAsync(Alice, Req("ethereum", TransactionType.Buy, 3m, 40m, 3));   // valor 150

        var summary = await Service().GetSummaryAsync(Alice);

        Assert.Equal(new[] { "ethereum", "bitcoin" }, summary.Positions.Select(p => p.Slug));   // por valor
        Assert.Equal(60m, summary.Positions[0].AllocationPct!.Value, 6);
        Assert.Equal(40m, summary.Positions[1].AllocationPct!.Value, 6);
    }

    [Fact]
    public async Task A_position_without_price_counts_for_cost_but_not_for_value_or_pnl()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddAssetAsync("obscure", "OBS", null);
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 60m, 3));
        await AddOkAsync(Alice, Req("obscure", TransactionType.Buy, 10m, 2m, 3));

        var summary = await Service().GetSummaryAsync(Alice);

        Assert.Equal(1, summary.UnpricedPositions);
        Assert.Equal(100m, summary.ValueUsd);
        Assert.Equal(80m, summary.CostBasisUsd);          // 60 + 20
        Assert.Equal(40m, summary.UnrealizedPnlUsd);      // solo la moneda con precio: 100 − 60
        var obscure = summary.Positions.Single(p => p.Slug == "obscure");
        Assert.Null(obscure.ValueUsd);
        Assert.Null(obscure.UnrealizedPnlUsd);
        Assert.Null(obscure.AllocationPct);
        Assert.Equal("bitcoin", summary.Positions[0].Slug);   // sin precio, al final
    }

    [Fact]
    public async Task The_24h_change_uses_each_coins_own_percentage()
    {
        await AddAssetAsync("bitcoin", "BTC", 110m, change24h: 10m);   // ayer valía 100
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 2m, 90m, 3));

        var summary = await Service().GetSummaryAsync(Alice);

        Assert.Equal(20m, summary.Change24hUsd, 6);       // 220 ahora − 200 ayer
        Assert.Equal(10m, summary.Change24hPct!.Value, 6);
    }

    // ----------------------------------------------------------- validación

    [Fact]
    public async Task Selling_more_than_owned_is_rejected_with_a_readable_message()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 10));

        var result = await Service().AddTransactionAsync(Alice, Req("bitcoin", TransactionType.Sell, 1.5m, 80m, 5));

        Assert.Equal(TransactionFailure.Invalid, result.Failure);
        Assert.Contains("BTC", result.Errors.Single());
        Assert.Contains("0,5", result.Errors.Single());
        Assert.Single((await Service().ListTransactionsAsync(Alice, new TransactionListQuery())).Items);
    }

    [Fact]
    public async Task A_backdated_sale_before_the_purchase_is_rejected()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 5));

        var result = await Service().AddTransactionAsync(Alice, Req("bitcoin", TransactionType.Sell, 1m, 80m, 10));

        Assert.Equal(TransactionFailure.Invalid, result.Failure);
    }

    [Fact]
    public async Task An_unknown_asset_is_reported_as_not_found()
    {
        var result = await Service().AddTransactionAsync(Alice, Req("nope", TransactionType.Buy, 1m, 50m, 1));

        Assert.Equal(TransactionFailure.AssetNotFound, result.Failure);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_before_touching_the_database()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);

        var result = await Service().AddTransactionAsync(Alice, Req("bitcoin", TransactionType.Buy, 0m, 50m, 1));

        Assert.Equal(TransactionFailure.Invalid, result.Failure);
        Assert.Null((await Service().GetSummaryAsync(Alice)).PortfolioId);   // ni siquiera se creó el portafolio
    }

    [Fact]
    public async Task The_per_user_transaction_limit_is_enforced()
    {
        _options = new PortfolioOptions { MaxTransactionsPerUser = 1 };
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 3));

        var result = await Service().AddTransactionAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 2));

        Assert.Equal(TransactionFailure.Invalid, result.Failure);
        Assert.Contains("máximo", result.Errors.Single());
    }

    // ---------------------------------------------------- editar y borrar

    [Fact]
    public async Task Updating_a_transaction_changes_the_position()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var buy = await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 10));

        var result = await Service().UpdateTransactionAsync(Alice, buy.Id, Req("bitcoin", TransactionType.Buy, 3m, 40m, 10));

        Assert.True(result.Succeeded);
        var p = (await Service().GetSummaryAsync(Alice)).Positions.Single();
        Assert.Equal(3m, p.Quantity);
        Assert.Equal(120m, p.CostBasisUsd);
    }

    [Fact]
    public async Task An_edit_that_would_leave_a_later_sale_uncovered_is_rejected_and_nothing_changes()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var buy = await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 2m, 50m, 10));
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Sell, 1.5m, 80m, 5));

        var result = await Service().UpdateTransactionAsync(Alice, buy.Id, Req("bitcoin", TransactionType.Buy, 1m, 50m, 10));

        Assert.Equal(TransactionFailure.Invalid, result.Failure);
        var stored = (await Service().ListTransactionsAsync(Alice, new TransactionListQuery())).Items.Single(t => t.Id == buy.Id);
        Assert.Equal(2m, stored.Quantity);
    }

    [Fact]
    public async Task Moving_a_transaction_to_another_coin_checks_both_histories()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddAssetAsync("ethereum", "ETH", 50m);
        var buy = await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 10));
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Sell, 1m, 80m, 5));

        // Sin esa compra de BTC, la venta de BTC quedaría al descubierto.
        var result = await Service().UpdateTransactionAsync(Alice, buy.Id, Req("ethereum", TransactionType.Buy, 1m, 50m, 10));

        Assert.Equal(TransactionFailure.Invalid, result.Failure);
    }

    [Fact]
    public async Task Deleting_a_purchase_that_a_later_sale_depends_on_is_rejected()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var buy = await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 10));
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Sell, 1m, 80m, 5));

        var result = await Service().DeleteTransactionAsync(Alice, buy.Id);

        Assert.Equal(TransactionFailure.Invalid, result.Failure);
        Assert.StartsWith("No se puede borrar", result.Errors.Single());
        Assert.Equal(2, (await Service().ListTransactionsAsync(Alice, new TransactionListQuery())).TotalCount);
    }

    [Fact]
    public async Task Deleting_a_free_transaction_works()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var buy = await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 10));

        var result = await Service().DeleteTransactionAsync(Alice, buy.Id);

        Assert.True(result.Succeeded);
        Assert.Empty((await Service().GetSummaryAsync(Alice)).Positions);
    }

    // ---------------------------------------------------------- aislamiento

    [Fact]
    public async Task Users_cannot_see_edit_or_delete_each_others_transactions()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var alices = await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 10));

        Assert.Empty((await Service().GetSummaryAsync(Bob)).Positions);
        Assert.Equal(0, (await Service().ListTransactionsAsync(Bob, new TransactionListQuery())).TotalCount);

        var update = await Service().UpdateTransactionAsync(Bob, alices.Id, Req("bitcoin", TransactionType.Buy, 9m, 1m, 10));
        var delete = await Service().DeleteTransactionAsync(Bob, alices.Id);

        Assert.Equal(TransactionFailure.NotFound, update.Failure);
        Assert.Equal(TransactionFailure.NotFound, delete.Failure);
        Assert.Equal(1m, (await Service().GetSummaryAsync(Alice)).Positions.Single().Quantity);
    }

    [Fact]
    public async Task Each_user_gets_a_separate_portfolio()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 10));
        await AddOkAsync(Bob, Req("bitcoin", TransactionType.Buy, 5m, 50m, 10));

        var a = await Service().GetSummaryAsync(Alice);
        var b = await Service().GetSummaryAsync(Bob);

        Assert.NotEqual(a.PortfolioId, b.PortfolioId);
        Assert.Equal(1m, a.Positions.Single().Quantity);
        Assert.Equal(5m, b.Positions.Single().Quantity);
    }

    // ------------------------------------------------------------- listado

    [Fact]
    public async Task Transactions_are_listed_newest_first_filtered_and_paged()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddAssetAsync("ethereum", "ETH", 50m);
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 50m, 10));
        await AddOkAsync(Alice, Req("ethereum", TransactionType.Buy, 1m, 50m, 8));
        await AddOkAsync(Alice, Req("bitcoin", TransactionType.Buy, 1m, 60m, 6));

        var all = await Service().ListTransactionsAsync(Alice, new TransactionListQuery());
        var onlyBtc = await Service().ListTransactionsAsync(Alice, new TransactionListQuery(AssetSlug: "Bitcoin"));
        var secondPage = await Service().ListTransactionsAsync(Alice, new TransactionListQuery(Page: 2, PageSize: 2));

        Assert.Equal(3, all.TotalCount);
        Assert.Equal(new[] { "bitcoin", "ethereum", "bitcoin" }, all.Items.Select(t => t.AssetSlug));
        Assert.Equal(60m, all.Items[0].UnitPriceUsd);
        Assert.Equal(2, onlyBtc.TotalCount);
        Assert.Single(secondPage.Items);
        Assert.Equal(2, secondPage.TotalPages);
    }

    [Fact]
    public async Task The_page_size_is_capped()
    {
        var result = await Service().ListTransactionsAsync(Alice, new TransactionListQuery(PageSize: 100_000));

        Assert.Equal(TransactionListQuery.MaxPageSize, result.PageSize);
    }
}
