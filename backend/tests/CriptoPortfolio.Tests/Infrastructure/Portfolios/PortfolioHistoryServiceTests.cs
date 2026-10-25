using CriptoPortfolio.Application.Catalog;
using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Domain;
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

/// <summary>
/// Reloj fijo: 14-oct-2026 12:00 UTC (hoy = 14, ayer = 13). Bitcoin cierra a 100 + día del mes (el 1 → 101 ... el 13 → 113)
/// y su precio actual es 120. Alice compra 2 BTC a 100 el 5 de octubre.
/// </summary>
public class PortfolioHistoryServiceTests : IDisposable
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly LivePriceCache _cache = new();
    private PortfolioHistoryOptions _options = new();

    public PortfolioHistoryServiceTests()
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

        var asset = new Asset
        {
            Slug = "bitcoin",
            Symbol = "BTC",
            Name = "Bitcoin",
            CoinGeckoId = "bitcoin",
            IsActive = true,
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now,
            Price = new AssetPrice { PriceUsd = 120m, Source = DataSource.CoinGecko, UpdatedAtUtc = Now.AddMinutes(-5) }
        };
        context.Assets.Add(asset);
        context.SaveChanges();
        BtcId = asset.Id;

        for (var day = 1; day <= 13; day++)
        {
            context.AssetDailyPrices.Add(new AssetDailyPrice
            {
                AssetId = BtcId,
                DateUtc = new DateOnly(2026, 10, day),
                CloseUsd = 100m + day,
                Source = DataSource.CoinGecko
            });
        }

        context.SaveChanges();
    }

    private int BtcId { get; }

    public void Dispose() => _db.Dispose();

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private static DateOnly D(int day) => new(2026, 10, day);

    private PortfolioHistoryService History(AppDbContext context) => new(
        context, _cache,
        new SyncLockService(context, _time, NullLogger<SyncLockService>.Instance),
        _time,
        Options.Create(new LivePriceOptions { StaleSeconds = 60 }),
        Options.Create(_options),
        NullLogger<PortfolioHistoryService>.Instance);

    private PortfolioService Portfolio(AppDbContext context) => new(
        context, _cache, new FifoCostBasisStrategy(), _time,
        Options.Create(new LivePriceOptions { StaleSeconds = 60 }),
        Options.Create(new PortfolioOptions()),
        NullLogger<PortfolioService>.Instance,
        History(context));

    private async Task<TransactionDto> BuyAsync(Guid user, decimal qty, decimal price, int dayOfOctober)
    {
        await using var context = _db.CreateContext();
        var result = await Portfolio(context).AddTransactionAsync(user, new TransactionRequest(
            "bitcoin", TransactionType.Buy, qty, price, null, new DateTime(2026, 10, dayOfOctober, 12, 0, 0, DateTimeKind.Utc), null));
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Transaction!;
    }

    private async Task<PortfolioHistory> ReadAsync(ChartRange range = ChartRange.All, Guid? user = null)
    {
        await using var context = _db.CreateContext();
        return await History(context).GetHistoryAsync(user ?? Alice, range);
    }

    private async Task<List<PortfolioDailyValue>> RowsAsync()
    {
        await using var context = _db.CreateContext();
        return await context.PortfolioDailyValues.AsNoTracking().OrderBy(v => v.DateUtc).ToListAsync();
    }

    [Fact]
    public async Task Without_transactions_the_history_is_empty()
    {
        var history = await ReadAsync();

        Assert.Null(history.PortfolioId);
        Assert.Empty(history.Points);
        Assert.Null(history.FirstTransactionDate);
        Assert.False(history.IncludesCurrentValue);
    }

    [Fact]
    public async Task The_chart_starts_on_the_first_transaction_and_ends_with_the_value_of_now()
    {
        await BuyAsync(Alice, 2m, 100m, 5);

        var history = await ReadAsync();

        Assert.Equal(D(5), history.FirstTransactionDate);
        Assert.Equal(10, history.Points.Count); // 5..13 y hoy
        Assert.Equal(new PortfolioHistoryPoint(D(5), 210m, 200m), history.Points[0]);
        Assert.Equal(new PortfolioHistoryPoint(D(13), 226m, 200m), history.Points[^2]);
        Assert.Equal(new PortfolioHistoryPoint(D(14), 240m, 200m), history.Points[^1]); // 2 × 120, el precio actual
        Assert.True(history.IncludesCurrentValue);
    }

    [Fact]
    public async Task The_first_read_saves_the_daily_snapshots_up_to_yesterday_but_not_today()
    {
        await BuyAsync(Alice, 2m, 100m, 5);

        await ReadAsync();
        var rows = await RowsAsync();

        Assert.Equal(9, rows.Count);
        Assert.Equal(D(5), rows[0].DateUtc);
        Assert.Equal(D(13), rows[^1].DateUtc);
        Assert.Equal(210m, rows[0].ValueUsd);
    }

    [Fact]
    public async Task Once_the_snapshots_are_complete_the_chart_is_read_from_the_table()
    {
        await BuyAsync(Alice, 2m, 100m, 5);
        await ReadAsync();

        await using (var context = _db.CreateContext())
        {
            var row = await context.PortfolioDailyValues.SingleAsync(v => v.DateUtc == D(7));
            row.ValueUsd = 999m;
            await context.SaveChangesAsync();
        }

        var history = await ReadAsync();

        Assert.Equal(999m, history.Points.Single(p => p.Date == D(7)).ValueUsd); // lo guardado, no recalculado
    }

    [Fact]
    public async Task Adding_an_older_transaction_discards_the_snapshots_from_its_date_and_they_are_rebuilt()
    {
        await BuyAsync(Alice, 2m, 100m, 5);
        await ReadAsync();

        await BuyAsync(Alice, 1m, 100m, 8);
        var afterEdit = await RowsAsync();
        Assert.Equal(new[] { D(5), D(6), D(7) }, afterEdit.Select(r => r.DateUtc)); // del 8 en adelante se borró

        var history = await ReadAsync();

        Assert.Equal(new PortfolioHistoryPoint(D(7), 214m, 200m), history.Points.Single(p => p.Date == D(7)));
        Assert.Equal(new PortfolioHistoryPoint(D(8), 324m, 300m), history.Points.Single(p => p.Date == D(8))); // 3 × 108
        Assert.Equal(9, (await RowsAsync()).Count);
    }

    [Fact]
    public async Task Deleting_the_oldest_transaction_moves_the_start_of_the_chart()
    {
        var first = await BuyAsync(Alice, 2m, 100m, 5);
        await BuyAsync(Alice, 1m, 100m, 9);
        await ReadAsync();

        await using (var context = _db.CreateContext())
        {
            var deleted = await Portfolio(context).DeleteTransactionAsync(Alice, first.Id);
            Assert.True(deleted.Succeeded);
        }

        var history = await ReadAsync();

        Assert.Equal(D(9), history.FirstTransactionDate);
        Assert.Equal(D(9), history.Points[0].Date);
        Assert.All(await RowsAsync(), r => Assert.True(r.DateUtc >= D(9)));
    }

    [Fact]
    public async Task Moving_a_transaction_to_an_earlier_date_discards_from_the_earlier_one()
    {
        await BuyAsync(Alice, 2m, 100m, 5);
        var later = await BuyAsync(Alice, 1m, 100m, 9);
        await ReadAsync();

        await using (var context = _db.CreateContext())
        {
            var updated = await Portfolio(context).UpdateTransactionAsync(Alice, later.Id, new TransactionRequest(
                "bitcoin", TransactionType.Buy, 1m, 100m, null, new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc), null));
            Assert.True(updated.Succeeded);
        }

        Assert.Equal(new[] { D(5), D(6) }, (await RowsAsync()).Select(r => r.DateUtc));
        var history = await ReadAsync();
        Assert.Equal(new PortfolioHistoryPoint(D(7), 321m, 300m), history.Points.Single(p => p.Date == D(7))); // 3 × 107
    }

    [Fact]
    public async Task A_day_is_not_saved_until_the_coin_has_its_close_but_the_chart_still_shows_it()
    {
        await BuyAsync(Alice, 2m, 100m, 5);
        await using (var context = _db.CreateContext())
        {
            context.AssetDailyPrices.Remove(await context.AssetDailyPrices.SingleAsync(d => d.DateUtc == D(13)));
            await context.SaveChangesAsync();
        }

        var history = await ReadAsync();

        Assert.Equal(D(12), (await RowsAsync())[^1].DateUtc); // el 13 aún no tiene cierre: no se guarda
        Assert.Equal(224m, history.Points.Single(p => p.Date == D(13)).ValueUsd); // pero se ve, con el cierre del 12
    }

    [Fact]
    public async Task A_coin_stuck_for_longer_than_the_limit_does_not_block_the_snapshots()
    {
        await BuyAsync(Alice, 2m, 100m, 5);
        await using (var context = _db.CreateContext())
        {
            context.AssetDailyPrices.RemoveRange(await context.AssetDailyPrices.Where(d => d.DateUtc > D(8)).ToListAsync());
            await context.SaveChangesAsync();
        }

        await ReadAsync();

        Assert.Equal(D(13), (await RowsAsync())[^1].DateUtc); // último cierre el 8: más de 3 días atrás, se ignora
    }

    [Fact]
    public async Task Ranges_limit_the_days_and_report_the_profit_of_the_period()
    {
        await BuyAsync(Alice, 2m, 100m, 5);

        var week = await ReadAsync(ChartRange.Week);

        Assert.Equal(D(7), week.Points[0].Date);   // hoy − 7 días
        Assert.Equal(8, week.Points.Count);
        Assert.Equal("7D", week.Range);
        Assert.Equal(26m, week.PeriodProfitUsd);   // 240 − 214, sin aportaciones nuevas
    }

    [Fact]
    public async Task Money_added_during_the_period_is_not_counted_as_profit()
    {
        await BuyAsync(Alice, 2m, 100m, 5);
        await BuyAsync(Alice, 1m, 100m, 10);

        var week = await ReadAsync(ChartRange.Week);

        // Valor: 3 × 120 − 2 × 107 = 146. Aportado en el periodo: 100. Ganancia: 46.
        Assert.Equal(46m, week.PeriodProfitUsd);
    }

    [Fact]
    public async Task Each_user_only_sees_their_own_history()
    {
        await BuyAsync(Alice, 2m, 100m, 5);

        var bob = await ReadAsync(user: Bob);

        Assert.Null(bob.PortfolioId);
        Assert.Empty(bob.Points);
    }

    [Fact]
    public async Task A_fresh_live_price_sets_the_value_of_today()
    {
        await BuyAsync(Alice, 2m, 100m, 5);
        _cache.SetRange(new[] { new KeyValuePair<int, CriptoPortfolio.Application.Prices.LivePrice>(BtcId, new(150m, Now)) });

        var history = await ReadAsync();

        Assert.Equal(300m, history.Points[^1].ValueUsd);
    }

    [Fact]
    public async Task The_worker_pass_completes_the_snapshots_and_the_next_one_has_nothing_to_do()
    {
        await BuyAsync(Alice, 2m, 100m, 5);

        await using (var context = _db.CreateContext())
        {
            var first = await History(context).SnapshotAllAsync("A");
            Assert.True(first.Ran);
            Assert.Equal(1, first.Portfolios);
            Assert.Equal(1, first.Rebuilt);
        }

        Assert.Equal(9, (await RowsAsync()).Count);

        await using (var context = _db.CreateContext())
        {
            var second = await History(context).SnapshotAllAsync("A");
            Assert.Equal(0, second.Rebuilt);
        }
    }

    [Fact]
    public async Task The_worker_pass_is_skipped_while_another_instance_holds_the_lock()
    {
        await BuyAsync(Alice, 2m, 100m, 5);
        await using (var context = _db.CreateContext())
        {
            var locks = new SyncLockService(context, _time, NullLogger<SyncLockService>.Instance);
            Assert.True(await locks.TryAcquireAsync(SyncJobNames.PortfolioSnapshots, "other", TimeSpan.FromMinutes(5)));
        }

        await using var mine = _db.CreateContext();
        var result = await History(mine).SnapshotAllAsync("A");

        Assert.False(result.Ran);
        Assert.Empty(await RowsAsync());
    }
}
