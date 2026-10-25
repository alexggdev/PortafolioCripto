using CriptoPortfolio.Application.Alerts;
using CriptoPortfolio.Application.Prices;
using CriptoPortfolio.Domain;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Infrastructure.Alerts;
using CriptoPortfolio.Infrastructure.Identity;
using CriptoPortfolio.Infrastructure.Persistence;
using CriptoPortfolio.Infrastructure.Prices;
using CriptoPortfolio.Infrastructure.Sync;
using CriptoPortfolio.Tests.Infrastructure.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Alerts;

public class PriceAlertTests : IDisposable
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly LivePriceCache _cache = new();
    private readonly PriceAlertOptions _options = new();
    private readonly int _btc;

    public PriceAlertTests()
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

        var btc = new Asset
        {
            Slug = "bitcoin", Symbol = "BTC", Name = "Bitcoin", CoinGeckoId = "bitcoin",
            IsActive = true, CreatedAtUtc = Now, UpdatedAtUtc = Now,
            Price = new AssetPrice { PriceUsd = 100m, Source = DataSource.CoinGecko, UpdatedAtUtc = Now.AddMinutes(-5) }
        };
        context.Assets.Add(btc);
        context.Assets.Add(new Asset
        {
            Slug = "sin-precio", Symbol = "NOP", Name = "Sin precio", CoinGeckoId = "sin-precio",
            IsActive = true, CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        context.Assets.Add(new Asset
        {
            Slug = "muerta", Symbol = "DEAD", Name = "Muerta", CoinGeckoId = "muerta",
            IsActive = false, CreatedAtUtc = Now, UpdatedAtUtc = Now,
            Price = new AssetPrice { PriceUsd = 1m, Source = DataSource.CoinGecko, UpdatedAtUtc = Now }
        });
        context.SaveChanges();
        _btc = btc.Id;
    }

    public void Dispose() => _db.Dispose();

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private AlertPriceReader Reader(AppDbContext context) => new(
        context, _cache, _time,
        Options.Create(new LivePriceOptions { StaleSeconds = 60 }),
        Options.Create(_options));

    private PriceAlertService Service()
    {
        var context = _db.CreateContext();
        return new PriceAlertService(context, Reader(context), _time, Options.Create(_options));
    }

    private (PriceAlertEvaluator Evaluator, SyncLockService Locks) EvaluatorWithLocks()
    {
        var context = _db.CreateContext();
        var locks = new SyncLockService(context, _time, NullLogger<SyncLockService>.Instance);
        var evaluator = new PriceAlertEvaluator(
            context, Reader(context), locks, _time, Options.Create(_options), NullLogger<PriceAlertEvaluator>.Instance);
        return (evaluator, locks);
    }

    private PriceAlertEvaluator Evaluator() => EvaluatorWithLocks().Evaluator;

    private void SetLive(decimal price, int? ageSeconds = 0) =>
        _cache.SetRange(new[] { KeyValuePair.Create(_btc, new LivePrice(price, Now.AddSeconds(-(ageSeconds ?? 0)))) });

    private async Task<PriceAlertDto> CreateOkAsync(Guid user, AlertDirection direction, decimal target, string slug = "bitcoin")
    {
        var result = await Service().CreateAsync(user, new CreateAlertRequest(slug, direction, target));
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        return result.Alert!;
    }

    // ------------------------------------------------------------- crear

    [Fact]
    public async Task Create_returns_the_alert_with_the_current_price()
    {
        var alert = await CreateOkAsync(Alice, AlertDirection.Above, 150m);

        Assert.Equal("bitcoin", alert.AssetSlug);
        Assert.Equal(100m, alert.CurrentPriceUsd); // sin precio en vivo, el de SQL (de hace 5 min)
        Assert.Null(alert.TriggeredAtUtc);
        Assert.Single(await Service().ListAsync(Alice));
    }

    [Theory]
    [InlineData(null, 150, "moneda")]
    [InlineData("bitcoin", 0, "mayor que cero")]
    [InlineData("bitcoin", -5, "mayor que cero")]
    [InlineData("bitcoin", 10000000000, "demasiado grande")]
    public async Task Create_rejects_invalid_input(string? slug, long target, string expectedText)
    {
        var result = await Service().CreateAsync(Alice, new CreateAlertRequest(slug, AlertDirection.Above, (decimal)target));

        Assert.Equal(AlertFailure.Invalid, result.Failure);
        Assert.Contains(result.Errors, e => e.Contains(expectedText));
    }

    [Fact]
    public async Task Create_rejects_more_than_18_decimals_and_unknown_direction()
    {
        var tooPrecise = await Service().CreateAsync(
            Alice, new CreateAlertRequest("bitcoin", AlertDirection.Above, 150.1234567890123456789m));
        Assert.Equal(AlertFailure.Invalid, tooPrecise.Failure);

        var badDirection = await Service().CreateAsync(Alice, new CreateAlertRequest("bitcoin", (AlertDirection)99, 150m));
        Assert.Equal(AlertFailure.Invalid, badDirection.Failure);
    }

    [Fact]
    public async Task Create_unknown_or_inactive_asset_is_not_found()
    {
        foreach (var slug in new[] { "no-existe", "muerta" })
        {
            var result = await Service().CreateAsync(Alice, new CreateAlertRequest(slug, AlertDirection.Above, 5m));
            Assert.Equal(AlertFailure.NotFound, result.Failure);
        }
    }

    [Fact]
    public async Task Create_rejects_a_target_that_is_already_met_including_equality()
    {
        var above = await Service().CreateAsync(Alice, new CreateAlertRequest("bitcoin", AlertDirection.Above, 90m));
        var aboveEqual = await Service().CreateAsync(Alice, new CreateAlertRequest("bitcoin", AlertDirection.Above, 100m));
        var below = await Service().CreateAsync(Alice, new CreateAlertRequest("bitcoin", AlertDirection.Below, 110m));
        var belowEqual = await Service().CreateAsync(Alice, new CreateAlertRequest("bitcoin", AlertDirection.Below, 100m));

        Assert.All(new[] { above, aboveEqual, below, belowEqual }, r => Assert.Equal(AlertFailure.Invalid, r.Failure));
        Assert.Empty(await Service().ListAsync(Alice));
    }

    [Fact]
    public async Task Create_needs_a_recent_price()
    {
        var noPrice = await Service().CreateAsync(Alice, new CreateAlertRequest("sin-precio", AlertDirection.Above, 5m));
        Assert.Equal(AlertFailure.Invalid, noPrice.Failure);

        // El precio de SQL tiene 5 minutos; con un máximo de 1 minuto ya no vale.
        _options.MaxDelayedPriceMinutes = 1;
        var stale = await Service().CreateAsync(Alice, new CreateAlertRequest("bitcoin", AlertDirection.Above, 150m));
        Assert.Equal(AlertFailure.Invalid, stale.Failure);
    }

    [Fact]
    public async Task Create_rejects_duplicates_and_stops_at_the_limit()
    {
        _options.MaxActivePerUser = 2;
        await CreateOkAsync(Alice, AlertDirection.Above, 150m);

        var duplicate = await Service().CreateAsync(Alice, new CreateAlertRequest("bitcoin", AlertDirection.Above, 150m));
        Assert.Equal(AlertFailure.Invalid, duplicate.Failure);

        await CreateOkAsync(Alice, AlertDirection.Above, 160m);
        var over = await Service().CreateAsync(Alice, new CreateAlertRequest("bitcoin", AlertDirection.Below, 50m));
        Assert.Equal(AlertFailure.LimitReached, over.Failure);

        // El límite es por usuario.
        await CreateOkAsync(Bob, AlertDirection.Above, 150m);
    }

    [Fact]
    public async Task Live_price_wins_over_sql_and_a_stale_live_price_is_ignored()
    {
        SetLive(120m);
        Assert.Equal(120m, (await CreateOkAsync(Alice, AlertDirection.Above, 200m)).CurrentPriceUsd);

        SetLive(999m, ageSeconds: 600); // vieja: se usa la de SQL
        var list = await Service().ListAsync(Alice);
        Assert.Equal(100m, list[0].CurrentPriceUsd);
    }

    // ------------------------------------------------------------- evaluar

    [Fact]
    public async Task Evaluate_triggers_only_the_alerts_that_are_met()
    {
        await CreateOkAsync(Alice, AlertDirection.Above, 150m);
        await CreateOkAsync(Alice, AlertDirection.Above, 120m);
        await CreateOkAsync(Alice, AlertDirection.Below, 80m);
        await CreateOkAsync(Alice, AlertDirection.Below, 60m);

        SetLive(125m);
        var first = await Evaluator().EvaluateAsync("w1");
        Assert.True(first.Ran);
        Assert.Equal(4, first.Checked);
        Assert.Equal(1, first.Triggered); // solo "sube a 120"

        SetLive(70m);
        var second = await Evaluator().EvaluateAsync("w1");
        Assert.Equal(3, second.Checked);
        Assert.Equal(1, second.Triggered); // "baja a 80"; "baja a 60" aún no

        var list = await Service().ListAsync(Alice);
        Assert.Equal(2, list.Count(a => a.TriggeredAtUtc != null));
        Assert.Equal(new[] { 125m, 70m }, list.Where(a => a.TriggeredAtUtc != null).OrderBy(a => a.TriggeredAtUtc).ThenBy(a => a.Id).Select(a => a.TriggeredPriceUsd!.Value));
    }

    [Fact]
    public async Task Evaluate_triggers_when_the_price_equals_the_target_and_only_once()
    {
        await CreateOkAsync(Alice, AlertDirection.Above, 150m);

        SetLive(150m);
        Assert.Equal(1, (await Evaluator().EvaluateAsync("w1")).Triggered);
        Assert.Equal(0, (await Evaluator().EvaluateAsync("w1")).Checked); // ya no está activa
        Assert.Equal(0, (await Evaluator().EvaluateAsync("w1")).Triggered);
    }

    [Fact]
    public async Task Evaluate_does_not_trigger_without_a_reliable_price()
    {
        await CreateOkAsync(Alice, AlertDirection.Above, 150m);

        // Precio en vivo viejo y precio de SQL demasiado viejo: ninguno es fiable.
        SetLive(500m, ageSeconds: 3600);
        _time.Advance(TimeSpan.FromMinutes(45));
        var result = await Evaluator().EvaluateAsync("w1");

        Assert.Equal(1, result.Checked);
        Assert.Equal(0, result.Triggered);
    }

    [Fact]
    public async Task Evaluate_is_skipped_while_another_instance_holds_the_lock()
    {
        await CreateOkAsync(Alice, AlertDirection.Above, 150m);
        SetLive(200m);

        var (evaluator, locks) = EvaluatorWithLocks();
        Assert.True(await locks.TryAcquireAsync(SyncJobNames.PriceAlerts, "otra-instancia", TimeSpan.FromMinutes(5)));

        var skipped = await evaluator.EvaluateAsync("w1");
        Assert.False(skipped.Ran);
        Assert.Equal(0, (await Service().UnreadCountAsync(Alice)));

        // Pasada la concesión, la toma otra instancia.
        _time.Advance(TimeSpan.FromMinutes(6));
        SetLive(200m); // el precio en vivo de antes ya estaría viejo: se renueva
        Assert.Equal(1, (await Evaluator().EvaluateAsync("w1")).Triggered);
    }

    [Fact]
    public async Task Evaluate_deletes_old_triggered_alerts()
    {
        await CreateOkAsync(Alice, AlertDirection.Above, 150m);
        SetLive(200m);
        await Evaluator().EvaluateAsync("w1");
        Assert.Single(await Service().ListAsync(Alice));

        _time.Advance(TimeSpan.FromDays(_options.KeepTriggeredDays + 1));
        await Evaluator().EvaluateAsync("w1");

        Assert.Empty(await Service().ListAsync(Alice));
    }

    // ------------------------------------------------------------- avisos y borrado

    [Fact]
    public async Task Unread_count_and_mark_read_work_per_user()
    {
        await CreateOkAsync(Alice, AlertDirection.Above, 150m);
        await CreateOkAsync(Alice, AlertDirection.Above, 160m);
        await CreateOkAsync(Bob, AlertDirection.Above, 150m);
        SetLive(165m); // supera 150 y 160
        await Evaluator().EvaluateAsync("w1");

        Assert.Equal(2, await Service().UnreadCountAsync(Alice));
        Assert.Equal(1, await Service().UnreadCountAsync(Bob));
        Assert.Contains(await Service().ListAsync(Alice), a => a.Unread);

        await Service().MarkAllReadAsync(Alice);

        Assert.Equal(0, await Service().UnreadCountAsync(Alice));
        Assert.Equal(1, await Service().UnreadCountAsync(Bob));
        Assert.DoesNotContain(await Service().ListAsync(Alice), a => a.Unread);
    }

    [Fact]
    public async Task Delete_only_removes_the_users_own_alert()
    {
        var alice = await CreateOkAsync(Alice, AlertDirection.Above, 150m);

        Assert.False(await Service().DeleteAsync(Bob, alice.Id));
        Assert.Single(await Service().ListAsync(Alice));

        Assert.True(await Service().DeleteAsync(Alice, alice.Id));
        Assert.False(await Service().DeleteAsync(Alice, alice.Id));
        Assert.Empty(await Service().ListAsync(Alice));
    }

    [Fact]
    public async Task List_is_private_per_user()
    {
        await CreateOkAsync(Alice, AlertDirection.Above, 150m);
        Assert.Empty(await Service().ListAsync(Bob));
    }
}
