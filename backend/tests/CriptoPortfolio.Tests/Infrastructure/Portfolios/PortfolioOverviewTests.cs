using CriptoPortfolio.Application.Portfolios;
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

/// <summary>La vista "Resumen": todos los portafolios del usuario sumados.</summary>
public class PortfolioOverviewTests : IDisposable
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));

    public PortfolioOverviewTests()
    {
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
            context, new LivePriceCache(), new FifoCostBasisStrategy(), _time,
            Options.Create(new LivePriceOptions { StaleSeconds = 60 }),
            Options.Create(new PortfolioOptions()),
            NullLogger<PortfolioService>.Instance);
    }

    private async Task AddAssetAsync(string slug, string symbol, decimal price, decimal? change1h = null, decimal? change7d = null)
    {
        await using var context = _db.CreateContext();
        context.Assets.Add(new Asset
        {
            Slug = slug,
            Symbol = symbol,
            Name = slug,
            CoinGeckoId = slug,
            IsActive = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
            Price = new AssetPrice
            {
                PriceUsd = price,
                Change1hPct = change1h,
                Change7dPct = change7d,
                Source = DataSource.CoinGecko,
                UpdatedAtUtc = Now.AddMinutes(-5)
            }
        });
        await context.SaveChangesAsync();
    }

    // El orden de los portafolios se fija con la fecha de creación, para que "el primero" no dependa del azar.
    private async Task<Guid> CreatePortfolioAsync(Guid user, string name, int createdDaysAgo)
    {
        await using var context = _db.CreateContext();
        var portfolio = new Portfolio { Id = Guid.NewGuid(), UserId = user, Name = name, CreatedAtUtc = Now.AddDays(-createdDaysAgo) };
        context.Portfolios.Add(portfolio);
        await context.SaveChangesAsync();
        return portfolio.Id;
    }

    private async Task BuyAsync(Guid user, Guid portfolio, string slug, decimal qty, decimal price, int daysAgo)
    {
        var result = await Service().AddTransactionAsync(
            user, new TransactionRequest(slug, TransactionType.Buy, qty, price, null, Now.AddDays(-daysAgo), null, portfolio));
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
    }

    [Fact]
    public async Task Overview_sums_every_portfolio_and_merges_the_same_coin()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddAssetAsync("ethereum", "ETH", 10m);
        var p1 = await CreatePortfolioAsync(Alice, "Uno", 2);
        var p2 = await CreatePortfolioAsync(Alice, "Dos", 1);
        await BuyAsync(Alice, p1, "bitcoin", 1m, 50m, 5);
        await BuyAsync(Alice, p1, "ethereum", 2m, 5m, 5);
        await BuyAsync(Alice, p2, "bitcoin", 3m, 70m, 4);

        var overview = await Service().GetOverviewAsync(Alice);

        Assert.Equal(420m, overview.ValueUsd);          // 4 BTC * 100 + 2 ETH * 10
        Assert.Equal(270m, overview.CostBasisUsd);      // 50 + 10 + 210
        Assert.Equal(150m, overview.UnrealizedPnlUsd);
        Assert.Equal(new[] { "Uno", "Dos" }, overview.Portfolios.Select(x => x.Name).ToArray());
        Assert.Equal(overview.ValueUsd, overview.Portfolios.Sum(x => x.ValueUsd));

        var btc = overview.Positions.Single(x => x.Slug == "bitcoin");
        Assert.Equal(4m, btc.Quantity);
        Assert.Equal(65m, btc.AverageCostUsd);          // (50 + 210) / 4
        Assert.Equal(2, overview.Positions.Count);
    }

    [Fact]
    public async Task Overview_ignores_other_users_portfolios()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var mine = await CreatePortfolioAsync(Alice, "Mío", 2);
        var theirs = await CreatePortfolioAsync(Bob, "Suyo", 2);
        await BuyAsync(Alice, mine, "bitcoin", 1m, 50m, 3);
        await BuyAsync(Bob, theirs, "bitcoin", 9m, 50m, 3);

        var overview = await Service().GetOverviewAsync(Alice);

        Assert.Equal(100m, overview.ValueUsd);
        Assert.Equal("Mío", Assert.Single(overview.Portfolios).Name);
    }

    [Fact]
    public async Task Overview_of_a_user_without_portfolios_is_empty()
    {
        var overview = await Service().GetOverviewAsync(Alice);

        Assert.Equal(0m, overview.ValueUsd);
        Assert.Empty(overview.Portfolios);
        Assert.Empty(overview.Positions);
    }

    [Fact]
    public async Task Overview_says_which_portfolios_hold_each_coin()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddAssetAsync("ethereum", "ETH", 10m);
        var p1 = await CreatePortfolioAsync(Alice, "Uno", 2);
        var p2 = await CreatePortfolioAsync(Alice, "Dos", 1);
        await BuyAsync(Alice, p1, "bitcoin", 1m, 50m, 5);
        await BuyAsync(Alice, p2, "bitcoin", 3m, 70m, 4);
        await BuyAsync(Alice, p2, "ethereum", 2m, 5m, 4);

        var overview = await Service().GetOverviewAsync(Alice);

        Assert.Equal(new[] { p1, p2 }, overview.Holders["bitcoin"].Select(h => h.PortfolioId).ToArray());
        Assert.Equal(new[] { 1m, 3m }, overview.Holders["bitcoin"].Select(h => h.Quantity).ToArray());
        Assert.Equal(new[] { p2 }, overview.Holders["ethereum"].Select(h => h.PortfolioId).ToArray());
    }

    [Fact]
    public async Task A_portfolio_left_out_of_the_total_is_listed_but_does_not_add_up()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var p1 = await CreatePortfolioAsync(Alice, "Uno", 2);
        var p2 = await CreatePortfolioAsync(Alice, "Dos", 1);
        await BuyAsync(Alice, p1, "bitcoin", 1m, 50m, 5);
        await BuyAsync(Alice, p2, "bitcoin", 3m, 70m, 4);
        await using (var context = _db.CreateContext())
        {
            context.PortfolioSettings.Add(new PortfolioSetting { PortfolioId = p2, IncludeInTotal = false });
            await context.SaveChangesAsync();
        }

        var overview = await Service().GetOverviewAsync(Alice);

        Assert.Equal(100m, overview.ValueUsd);
        Assert.Equal(1m, Assert.Single(overview.Positions).Quantity);
        Assert.Equal(p1, Assert.Single(overview.Holders["bitcoin"]).PortfolioId);
        Assert.Equal(new[] { true, false }, overview.Portfolios.Select(x => x.IncludeInTotal).ToArray());
        Assert.Equal(300m, overview.Portfolios.Single(x => x.Id == p2).ValueUsd);
    }

    [Fact]
    public async Task Transactions_can_be_filtered_by_type()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var p1 = await CreatePortfolioAsync(Alice, "Uno", 2);
        await BuyAsync(Alice, p1, "bitcoin", 2m, 50m, 5);
        var sold = await Service().AddTransactionAsync(
            Alice, new TransactionRequest("bitcoin", TransactionType.Sell, 1m, 80m, null, Now.AddDays(-3), null, p1));
        Assert.True(sold.Succeeded, string.Join(" ", sold.Errors));

        var sells = await Service().ListTransactionsAsync(Alice, new TransactionListQuery(PortfolioId: p1, Type: TransactionType.Sell));
        var all = await Service().ListTransactionsAsync(Alice, new TransactionListQuery(PortfolioId: p1));

        Assert.Equal(TransactionType.Sell, Assert.Single(sells.Items).Type);
        Assert.Equal(2, all.TotalCount);
    }
}
