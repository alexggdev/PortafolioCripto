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

/// <summary>Quitar y mover monedas entre portafolios, y las variaciones de 1 h y 7 d de cada posición.</summary>
public class PortfolioAssetActionsTests : IDisposable
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));

    public PortfolioAssetActionsTests()
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
    public async Task Positions_carry_the_1h_and_7d_changes_of_their_coin()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m, change1h: 0.5m, change7d: -4.5m);
        var p = await CreatePortfolioAsync(Alice, "Uno", 2);
        await BuyAsync(Alice, p, "bitcoin", 1m, 50m, 3);

        var position = (await Service().GetSummaryAsync(Alice, p)).Positions.Single();

        Assert.Equal(0.5m, position.Change1hPct);
        Assert.Equal(-4.5m, position.Change7dPct);
    }

    [Fact]
    public async Task Removing_a_coin_deletes_only_its_transactions_in_that_portfolio()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddAssetAsync("ethereum", "ETH", 10m);
        var p1 = await CreatePortfolioAsync(Alice, "Uno", 2);
        var p2 = await CreatePortfolioAsync(Alice, "Dos", 1);
        await BuyAsync(Alice, p1, "bitcoin", 1m, 50m, 5);
        await BuyAsync(Alice, p1, "ethereum", 2m, 5m, 5);
        await BuyAsync(Alice, p2, "bitcoin", 3m, 60m, 4);

        var result = await Service().RemoveAssetAsync(Alice, "Bitcoin", p1);

        Assert.True(result.Succeeded);
        var first = await Service().GetSummaryAsync(Alice, p1);
        Assert.Equal("ethereum", Assert.Single(first.Positions).Slug);
        var second = await Service().GetSummaryAsync(Alice, p2);
        Assert.Equal(3m, Assert.Single(second.Positions).Quantity);
    }

    [Fact]
    public async Task Removing_a_coin_that_is_not_in_the_portfolio_is_not_found()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddAssetAsync("ethereum", "ETH", 10m);
        var p = await CreatePortfolioAsync(Alice, "Uno", 2);
        await BuyAsync(Alice, p, "bitcoin", 1m, 50m, 5);

        var result = await Service().RemoveAssetAsync(Alice, "ethereum", p);

        Assert.Equal(TransactionFailure.NotFound, result.Failure);
    }

    [Fact]
    public async Task A_user_cannot_remove_coins_from_someone_elses_portfolio()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var p = await CreatePortfolioAsync(Alice, "Uno", 2);
        await BuyAsync(Alice, p, "bitcoin", 1m, 50m, 5);

        var result = await Service().RemoveAssetAsync(Bob, "bitcoin", p);

        Assert.Equal(TransactionFailure.NotFound, result.Failure);
        Assert.Single((await Service().GetSummaryAsync(Alice, p)).Positions);
    }

    [Fact]
    public async Task Moving_a_coin_takes_its_whole_history_to_the_other_portfolio()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        await AddAssetAsync("ethereum", "ETH", 10m);
        var p1 = await CreatePortfolioAsync(Alice, "Uno", 2);
        var p2 = await CreatePortfolioAsync(Alice, "Dos", 1);
        await BuyAsync(Alice, p1, "bitcoin", 1m, 50m, 6);
        await BuyAsync(Alice, p1, "bitcoin", 1m, 70m, 5);
        await BuyAsync(Alice, p1, "ethereum", 2m, 5m, 5);

        var result = await Service().MoveAssetAsync(Alice, "bitcoin", p1, p2);

        Assert.True(result.Succeeded);
        var first = await Service().GetSummaryAsync(Alice, p1);
        Assert.Equal("ethereum", Assert.Single(first.Positions).Slug);
        var second = (await Service().GetSummaryAsync(Alice, p2)).Positions.Single();
        Assert.Equal(2m, second.Quantity);
        Assert.Equal(120m, second.CostBasisUsd);
        var moved = await Service().ListTransactionsAsync(Alice, new TransactionListQuery(PortfolioId: p2));
        Assert.Equal(2, moved.TotalCount);
    }

    [Fact]
    public async Task Moving_a_coin_into_a_portfolio_that_already_has_it_merges_the_positions()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var p1 = await CreatePortfolioAsync(Alice, "Uno", 2);
        var p2 = await CreatePortfolioAsync(Alice, "Dos", 1);
        await BuyAsync(Alice, p1, "bitcoin", 1m, 50m, 6);
        await BuyAsync(Alice, p2, "bitcoin", 2m, 60m, 5);

        var result = await Service().MoveAssetAsync(Alice, "bitcoin", p1, p2);

        Assert.True(result.Succeeded);
        Assert.Empty((await Service().GetSummaryAsync(Alice, p1)).Positions);
        Assert.Equal(3m, (await Service().GetSummaryAsync(Alice, p2)).Positions.Single().Quantity);
    }

    [Fact]
    public async Task Moving_to_the_same_portfolio_is_rejected()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var p = await CreatePortfolioAsync(Alice, "Uno", 2);
        await BuyAsync(Alice, p, "bitcoin", 1m, 50m, 5);

        var result = await Service().MoveAssetAsync(Alice, "bitcoin", p, p);

        Assert.Equal(TransactionFailure.Invalid, result.Failure);
    }

    [Fact]
    public async Task Moving_to_a_portfolio_of_another_user_is_not_found_and_changes_nothing()
    {
        await AddAssetAsync("bitcoin", "BTC", 100m);
        var mine = await CreatePortfolioAsync(Alice, "Uno", 2);
        var theirs = await CreatePortfolioAsync(Bob, "Ajeno", 2);
        await BuyAsync(Alice, mine, "bitcoin", 1m, 50m, 5);

        var result = await Service().MoveAssetAsync(Alice, "bitcoin", mine, theirs);

        Assert.Equal(TransactionFailure.NotFound, result.Failure);
        Assert.Single((await Service().GetSummaryAsync(Alice, mine)).Positions);
        Assert.Empty((await Service().GetSummaryAsync(Bob, theirs)).Positions);
    }
}
