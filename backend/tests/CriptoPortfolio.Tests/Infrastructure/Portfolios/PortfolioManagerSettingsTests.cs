using CriptoPortfolio.Application.Portfolios;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Domain.Enums;
using CriptoPortfolio.Infrastructure.Identity;
using CriptoPortfolio.Infrastructure.Persistence;
using CriptoPortfolio.Infrastructure.Portfolios;
using CriptoPortfolio.Tests.Infrastructure.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Portfolios;

/// <summary>Imagen, orden, «contabilizar», «predeterminado» y duplicar portafolios.</summary>
public class PortfolioManagerSettingsTests : IDisposable
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));

    public PortfolioManagerSettingsTests()
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
                CreatedAtUtc = _time.GetUtcNow().UtcDateTime
            });
        }

        context.Assets.Add(new Asset
        {
            Slug = "bitcoin",
            Symbol = "BTC",
            Name = "Bitcoin",
            CoinGeckoId = "bitcoin",
            IsActive = true,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
            UpdatedAtUtc = _time.GetUtcNow().UtcDateTime
        });
        context.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private PortfolioManager Manager(AppDbContext? context = null) =>
        new(context ?? _db.CreateContext(), _time, Options.Create(new PortfolioOptions()));

    private async Task<Guid> CreateAsync(Guid user, string name, int? avatar = null)
    {
        var result = await Manager().CreateAsync(user, name, avatar);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        _time.Advance(TimeSpan.FromMinutes(1)); // cada portafolio con su propia fecha de creación
        return result.Portfolio!.Id;
    }

    [Fact]
    public async Task A_new_portfolio_keeps_the_chosen_avatar_and_counts_in_the_total()
    {
        var id = await CreateAsync(Alice, "Largo plazo", avatar: 5);

        var listed = Assert.Single(await Manager().ListAsync(Alice));

        Assert.Equal(id, listed.Id);
        Assert.Equal(5, listed.Avatar);
        Assert.True(listed.IncludeInTotal);
        Assert.False(listed.IsDefault);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(PortfolioManager.AvatarCount)]
    public async Task An_avatar_out_of_range_is_rejected(int avatar)
    {
        var created = await Manager().CreateAsync(Alice, "X", avatar);
        var id = await CreateAsync(Alice, "Y");
        var updated = await Manager().UpdateAsync(Alice, id, new PortfolioChanges(Avatar: avatar));

        Assert.Equal(PortfolioFailure.Invalid, created.Failure);
        Assert.Equal(PortfolioFailure.Invalid, updated.Failure);
    }

    [Fact]
    public async Task Update_changes_only_what_is_sent()
    {
        var id = await CreateAsync(Alice, "Uno", avatar: 3);

        var result = await Manager().UpdateAsync(Alice, id, new PortfolioChanges(IncludeInTotal: false));

        Assert.True(result.Succeeded);
        Assert.Equal("Uno", result.Portfolio!.Name);
        Assert.Equal(3, result.Portfolio.Avatar);
        Assert.False(result.Portfolio.IncludeInTotal);
    }

    [Fact]
    public async Task Update_can_rename_and_checks_the_name()
    {
        var id = await CreateAsync(Alice, "Uno");
        await CreateAsync(Alice, "Dos");

        var renamed = await Manager().UpdateAsync(Alice, id, new PortfolioChanges(Name: "  Nuevo "));
        var taken = await Manager().UpdateAsync(Alice, id, new PortfolioChanges(Name: "dos"));
        var empty = await Manager().UpdateAsync(Alice, id, new PortfolioChanges(Name: " "));

        Assert.Equal("Nuevo", renamed.Portfolio!.Name);
        Assert.Equal(PortfolioFailure.Invalid, taken.Failure);
        Assert.Equal(PortfolioFailure.Invalid, empty.Failure);
    }

    [Fact]
    public async Task Only_one_portfolio_is_the_default()
    {
        var one = await CreateAsync(Alice, "Uno");
        var two = await CreateAsync(Alice, "Dos");
        var other = await CreateAsync(Bob, "De Bob");

        await Manager().UpdateAsync(Alice, one, new PortfolioChanges(IsDefault: true));
        await Manager().UpdateAsync(Bob, other, new PortfolioChanges(IsDefault: true));
        await Manager().UpdateAsync(Alice, two, new PortfolioChanges(IsDefault: true));

        var mine = await Manager().ListAsync(Alice);
        Assert.Equal(new[] { two }, mine.Where(p => p.IsDefault).Select(p => p.Id).ToArray());
        Assert.True(Assert.Single(await Manager().ListAsync(Bob)).IsDefault); // lo de otro usuario no se toca
    }

    [Fact]
    public async Task Update_of_someone_elses_portfolio_is_not_found()
    {
        var id = await CreateAsync(Alice, "Uno");

        var result = await Manager().UpdateAsync(Bob, id, new PortfolioChanges(Name: "Mío"));

        Assert.Equal(PortfolioFailure.NotFound, result.Failure);
    }

    [Fact]
    public async Task Reorder_sets_the_order_of_the_list()
    {
        var one = await CreateAsync(Alice, "Uno");
        var two = await CreateAsync(Alice, "Dos");
        var three = await CreateAsync(Alice, "Tres");

        var result = await Manager().ReorderAsync(Alice, new[] { three, one, two });

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { three, one, two }, (await Manager().ListAsync(Alice)).Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task Reorder_needs_every_portfolio_exactly_once()
    {
        var one = await CreateAsync(Alice, "Uno");
        var two = await CreateAsync(Alice, "Dos");
        var foreign = await CreateAsync(Bob, "Ajeno");

        Assert.Equal(PortfolioFailure.Invalid, (await Manager().ReorderAsync(Alice, new[] { one })).Failure);
        Assert.Equal(PortfolioFailure.Invalid, (await Manager().ReorderAsync(Alice, new[] { one, one })).Failure);
        Assert.Equal(PortfolioFailure.Invalid, (await Manager().ReorderAsync(Alice, new[] { one, foreign })).Failure);
        Assert.Equal(new[] { one, two }, (await Manager().ListAsync(Alice)).Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task Duplicate_copies_transactions_avatar_and_total_setting()
    {
        var id = await CreateAsync(Alice, "Uno", avatar: 9);
        await Manager().UpdateAsync(Alice, id, new PortfolioChanges(IncludeInTotal: false, IsDefault: true));
        await using (var context = _db.CreateContext())
        {
            var asset = await context.Assets.SingleAsync();
            context.Transactions.Add(new Transaction
            {
                PortfolioId = id, AssetId = asset.Id, Type = TransactionType.Buy, Quantity = 2m, UnitPriceUsd = 50m,
                FeeUsd = 1m, DateUtc = _time.GetUtcNow().UtcDateTime.AddDays(-3), Note = "nota"
            });
            await context.SaveChangesAsync();
        }

        var result = await Manager().DuplicateAsync(Alice, id, "Uno copia");

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        var copy = result.Portfolio!;
        Assert.NotEqual(id, copy.Id);
        Assert.Equal("Uno copia", copy.Name);
        Assert.Equal(1, copy.TransactionCount);
        Assert.Equal(9, copy.Avatar);
        Assert.False(copy.IncludeInTotal);
        Assert.False(copy.IsDefault);

        await using var check = _db.CreateContext();
        var rows = await check.Transactions.Where(t => t.PortfolioId == copy.Id).ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal((2m, 50m, 1m, "nota"), (row.Quantity, row.UnitPriceUsd, row.FeeUsd, row.Note));
        Assert.Equal(1, await check.Transactions.CountAsync(t => t.PortfolioId == id)); // el original no cambia
    }

    [Fact]
    public async Task Duplicate_checks_the_name_and_the_owner()
    {
        var id = await CreateAsync(Alice, "Uno");

        Assert.Equal(PortfolioFailure.Invalid, (await Manager().DuplicateAsync(Alice, id, "uno")).Failure);
        Assert.Equal(PortfolioFailure.Invalid, (await Manager().DuplicateAsync(Alice, id, "")).Failure);
        Assert.Equal(PortfolioFailure.NotFound, (await Manager().DuplicateAsync(Bob, id, "Copia")).Failure);
    }

    [Fact]
    public async Task Deleting_a_portfolio_removes_its_settings()
    {
        var id = await CreateAsync(Alice, "Uno", avatar: 2);

        var result = await Manager().DeleteAsync(Alice, id);

        Assert.True(result.Succeeded);
        await using var check = _db.CreateContext();
        Assert.Empty(await check.PortfolioSettings.ToListAsync());
    }
}
