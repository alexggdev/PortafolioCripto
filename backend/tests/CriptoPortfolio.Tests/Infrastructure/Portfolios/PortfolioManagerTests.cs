using CriptoPortfolio.Application.Catalog;
using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Domain.CostBasis;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Infrastructure.Identity;
using CriptoPortfolio.Infrastructure.Persistence;
using CriptoPortfolio.Infrastructure.Portfolios;
using CriptoPortfolio.Infrastructure.Prices;
using CriptoPortfolio.Infrastructure.Sync;
using CriptoPortfolio.Tests.Infrastructure.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Portfolios;

public class PortfolioManagerTests : IDisposable
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly LivePriceCache _cache = new();
    private PortfolioOptions _options = new();

    public PortfolioManagerTests()
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

        context.Assets.Add(new Asset
        {
            Slug = "bitcoin",
            Symbol = "BTC",
            Name = "Bitcoin",
            CoinGeckoId = "bitcoin",
            IsActive = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
            Price = new AssetPrice { PriceUsd = 100m, Source = DataSource.CoinGecko, UpdatedAtUtc = Now.AddMinutes(-5) }
        });
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private PortfolioManager Manager(AppDbContext? context = null) =>
        new(context ?? _db.CreateContext(), _time, Options.Create(_options));

    private PortfolioService Service(AppDbContext? context = null) => new(
        context ?? _db.CreateContext(), _cache, new FifoCostBasisStrategy(), _time,
        Options.Create(new LivePriceOptions { StaleSeconds = 60 }),
        Options.Create(_options),
        NullLogger<PortfolioService>.Instance);

    private async Task<PortfolioInfo> CreateOkAsync(Guid user, string name)
    {
        _time.Advance(TimeSpan.FromSeconds(1)); // orden de creación estable
        var result = await Manager().CreateAsync(user, name);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Portfolio!;
    }

    private async Task<TransactionResult> BuyAsync(Guid user, decimal qty, Guid? portfolioId, int daysAgo = 3) =>
        await Service().AddTransactionAsync(user, new TransactionRequest(
            "bitcoin", TransactionType.Buy, qty, 100m, null, Now.AddDays(-daysAgo), null, portfolioId));

    // ------------------------------------------------------------- gestión

    [Fact]
    public async Task A_new_user_has_no_portfolios()
    {
        Assert.Empty(await Manager().ListAsync(Alice));
    }

    [Fact]
    public async Task Created_portfolios_are_listed_oldest_first_with_their_transaction_counts()
    {
        var first = await CreateOkAsync(Alice, "Largo plazo");
        var second = await CreateOkAsync(Alice, "Trading");
        Assert.True((await BuyAsync(Alice, 1m, second.Id)).Succeeded);
        Assert.True((await BuyAsync(Alice, 2m, second.Id)).Succeeded);

        var list = await Manager().ListAsync(Alice);

        Assert.Equal(new[] { "Largo plazo", "Trading" }, list.Select(p => p.Name));
        Assert.Equal(new[] { 0, 2 }, list.Select(p => p.TransactionCount));
        Assert.Equal(first.Id, list[0].Id);
    }

    [Fact]
    public async Task Names_are_trimmed_required_and_limited()
    {
        var ok = await Manager().CreateAsync(Alice, "  Cuenta  ");
        Assert.Equal("Cuenta", ok.Portfolio!.Name);

        foreach (var bad in new[] { "", "   ", null, new string('x', PortfolioManager.MaxNameLength + 1) })
        {
            var result = await Manager().CreateAsync(Alice, bad);
            Assert.Equal(PortfolioFailure.Invalid, result.Failure);
        }
    }

    [Fact]
    public async Task A_name_cannot_be_repeated_by_the_same_user_but_another_user_can_use_it()
    {
        await CreateOkAsync(Alice, "Trading");

        var again = await Manager().CreateAsync(Alice, "trading");
        Assert.Equal(PortfolioFailure.Invalid, again.Failure);
        Assert.Contains("Ya tienes", again.Errors[0]);

        Assert.True((await Manager().CreateAsync(Bob, "Trading")).Succeeded);
    }

    [Fact]
    public async Task The_number_of_portfolios_per_user_is_limited()
    {
        _options = new PortfolioOptions { MaxPortfoliosPerUser = 2 };
        await CreateOkAsync(Alice, "A");
        await CreateOkAsync(Alice, "B");

        var third = await Manager().CreateAsync(Alice, "C");

        Assert.Equal(PortfolioFailure.Invalid, third.Failure);
        Assert.Contains("máximo de 2", third.Errors[0]);
    }

    [Fact]
    public async Task Renaming_works_and_cannot_clash_with_another_of_your_portfolios()
    {
        var a = await CreateOkAsync(Alice, "A");
        await CreateOkAsync(Alice, "B");

        Assert.Equal("Nuevo", (await Manager().RenameAsync(Alice, a.Id, "Nuevo")).Portfolio!.Name);
        Assert.Equal(PortfolioFailure.Invalid, (await Manager().RenameAsync(Alice, a.Id, "b")).Failure);
        Assert.True((await Manager().RenameAsync(Alice, a.Id, "NUEVO")).Succeeded); // el mismo, solo cambian las mayúsculas
    }

    [Fact]
    public async Task Nobody_can_rename_or_delete_another_users_portfolio()
    {
        var a = await CreateOkAsync(Alice, "A");

        Assert.Equal(PortfolioFailure.NotFound, (await Manager().RenameAsync(Bob, a.Id, "Mío")).Failure);
        Assert.Equal(PortfolioFailure.NotFound, (await Manager().DeleteAsync(Bob, a.Id)).Failure);
        Assert.Single(await Manager().ListAsync(Alice));
    }

    [Fact]
    public async Task Deleting_a_portfolio_removes_its_transactions_and_keeps_the_rest()
    {
        var a = await CreateOkAsync(Alice, "A");
        var b = await CreateOkAsync(Alice, "B");
        Assert.True((await BuyAsync(Alice, 1m, a.Id)).Succeeded);
        Assert.True((await BuyAsync(Alice, 5m, b.Id)).Succeeded);

        Assert.True((await Manager().DeleteAsync(Alice, a.Id)).Succeeded);

        await using var context = _db.CreateContext();
        Assert.Equal(new[] { b.Id }, await context.Portfolios.Select(p => p.Id).ToListAsync());
        Assert.Equal(new[] { b.Id }, await context.Transactions.Select(t => t.PortfolioId).Distinct().ToListAsync());
    }

    // ------------------------------------------- varios portafolios en el servicio

    [Fact]
    public async Task Transactions_go_to_the_chosen_portfolio_and_each_summary_only_counts_its_own()
    {
        var a = await CreateOkAsync(Alice, "A");
        var b = await CreateOkAsync(Alice, "B");
        Assert.True((await BuyAsync(Alice, 1m, a.Id)).Succeeded);
        Assert.True((await BuyAsync(Alice, 4m, b.Id)).Succeeded);

        var summaryA = await Service().GetSummaryAsync(Alice, a.Id);
        var summaryB = await Service().GetSummaryAsync(Alice, b.Id);

        Assert.Equal(a.Id, summaryA.PortfolioId);
        Assert.Equal(1m, summaryA.Positions.Single().Quantity);
        Assert.Equal(4m, summaryB.Positions.Single().Quantity);
    }

    [Fact]
    public async Task Without_a_portfolio_id_the_first_portfolio_is_used()
    {
        var a = await CreateOkAsync(Alice, "A");
        await CreateOkAsync(Alice, "B");

        Assert.True((await BuyAsync(Alice, 2m, null)).Succeeded);

        Assert.Equal(2m, (await Service().GetSummaryAsync(Alice, a.Id)).Positions.Single().Quantity);
        Assert.Equal(a.Id, (await Service().GetSummaryAsync(Alice)).PortfolioId);
    }

    [Fact]
    public async Task The_first_transaction_without_a_portfolio_creates_the_default_one()
    {
        Assert.True((await BuyAsync(Alice, 1m, null)).Succeeded);

        var list = await Manager().ListAsync(Alice);
        Assert.Equal("Mi portafolio", Assert.Single(list).Name);
    }

    [Fact]
    public async Task Adding_to_a_portfolio_that_is_not_yours_or_does_not_exist_is_not_found()
    {
        var alices = await CreateOkAsync(Alice, "A");

        var stolen = await BuyAsync(Bob, 1m, alices.Id);
        var missing = await BuyAsync(Bob, 1m, Guid.NewGuid());

        Assert.Equal(TransactionFailure.NotFound, stolen.Failure);
        Assert.Equal(TransactionFailure.NotFound, missing.Failure);
        Assert.Empty(await Manager().ListAsync(Bob)); // y no se creó ninguno por el camino
        Assert.Empty((await Service().GetSummaryAsync(Alice, alices.Id)).Positions);
    }

    [Fact]
    public async Task Reading_someone_elses_portfolio_returns_nothing()
    {
        var alices = await CreateOkAsync(Alice, "A");
        Assert.True((await BuyAsync(Alice, 1m, alices.Id)).Succeeded);

        var summary = await Service().GetSummaryAsync(Bob, alices.Id);
        var transactions = await Service().ListTransactionsAsync(Bob, new TransactionListQuery(PortfolioId: alices.Id));

        Assert.Null(summary.PortfolioId);
        Assert.Empty(summary.Positions);
        Assert.Empty(transactions.Items);
    }

    [Fact]
    public async Task The_transaction_list_can_be_filtered_by_portfolio()
    {
        var a = await CreateOkAsync(Alice, "A");
        var b = await CreateOkAsync(Alice, "B");
        Assert.True((await BuyAsync(Alice, 1m, a.Id)).Succeeded);
        Assert.True((await BuyAsync(Alice, 4m, b.Id)).Succeeded);

        var list = await Service().ListTransactionsAsync(Alice, new TransactionListQuery(PortfolioId: b.Id));

        Assert.Equal(4m, Assert.Single(list.Items).Quantity);
    }

    [Fact]
    public async Task The_transaction_limit_counts_all_of_a_users_portfolios()
    {
        _options = new PortfolioOptions { MaxTransactionsPerUser = 2 };
        var a = await CreateOkAsync(Alice, "A");
        var b = await CreateOkAsync(Alice, "B");
        Assert.True((await BuyAsync(Alice, 1m, a.Id)).Succeeded);
        Assert.True((await BuyAsync(Alice, 1m, b.Id)).Succeeded);

        var third = await BuyAsync(Alice, 1m, b.Id);

        Assert.Equal(TransactionFailure.Invalid, third.Failure);
        Assert.True((await BuyAsync(Bob, 1m, null)).Succeeded); // el tope es por usuario
    }

    [Fact]
    public async Task Each_portfolio_has_its_own_history()
    {
        var a = await CreateOkAsync(Alice, "A");
        var b = await CreateOkAsync(Alice, "B");
        Assert.True((await BuyAsync(Alice, 1m, a.Id, daysAgo: 5)).Succeeded);
        Assert.True((await BuyAsync(Alice, 3m, b.Id, daysAgo: 2)).Succeeded);

        await using var context = _db.CreateContext();
        var history = new PortfolioHistoryService(
            context, _cache, new SyncLockService(context, _time, NullLogger<SyncLockService>.Instance), _time,
            Options.Create(new LivePriceOptions { StaleSeconds = 60 }),
            Options.Create(new PortfolioHistoryOptions()),
            NullLogger<PortfolioHistoryService>.Instance);

        var ha = await history.GetHistoryAsync(Alice, ChartRange.All, a.Id);
        var hb = await history.GetHistoryAsync(Alice, ChartRange.All, b.Id);
        var stranger = await history.GetHistoryAsync(Bob, ChartRange.All, a.Id);

        Assert.Equal(a.Id, ha.PortfolioId);
        Assert.Equal(new DateOnly(2026, 10, 9), ha.FirstTransactionDate);
        Assert.Equal(new DateOnly(2026, 10, 12), hb.FirstTransactionDate);
        Assert.Empty(stranger.Points);
    }

    // ------------------------------------------------------------- exportación

    [Fact]
    public async Task Export_returns_one_portfolio_oldest_first_and_never_another_users()
    {
        var a = await CreateOkAsync(Alice, "Largo plazo");
        var b = await CreateOkAsync(Alice, "Trading");
        await BuyAsync(Alice, 1m, a.Id, daysAgo: 2);
        await BuyAsync(Alice, 2m, a.Id, daysAgo: 5);
        await BuyAsync(Alice, 9m, b.Id);
        var bobs = await CreateOkAsync(Bob, "Mio");
        await BuyAsync(Bob, 7m, bobs.Id);

        var export = await Service().ExportTransactionsAsync(Alice, a.Id);

        Assert.Equal(new[] { 2m, 1m }, export.Select(t => t.Quantity));
        Assert.True(export[0].DateUtc < export[1].DateUtc);

        // Sin id: el primer portafolio. Con el id de otro usuario: vacío.
        Assert.Equal(2, (await Service().ExportTransactionsAsync(Alice)).Count);
        Assert.Empty(await Service().ExportTransactionsAsync(Alice, bobs.Id));
        Assert.Empty(await Service().ExportTransactionsAsync(Guid.NewGuid()));
    }
}
