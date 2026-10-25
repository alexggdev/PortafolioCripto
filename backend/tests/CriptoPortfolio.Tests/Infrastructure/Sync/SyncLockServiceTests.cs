using CriptoPortfolio.Domain;
using CriptoPortfolio.Domain.Entities;
using CriptoPortfolio.Infrastructure.Sync;
using CriptoPortfolio.Tests.Infrastructure.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CriptoPortfolio.Tests.Infrastructure.Sync;

public class SyncLockServiceTests : IDisposable
{
    private const string Job = SyncJobNames.Catalog;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private readonly SqliteTestDb _db = new();
    private readonly FakeTimeProvider _time =
        new(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

    public void Dispose() => _db.Dispose();

    // Un servicio (y un DbContext) nuevo por llamada, como harían dos instancias distintas del backend.
    private SyncLockService NewService() =>
        new(_db.CreateContext(), _time, NullLogger<SyncLockService>.Instance);

    private async Task<SyncState> ReadStateAsync()
    {
        await using var context = _db.CreateContext();
        return await context.SyncStates.AsNoTracking().SingleAsync(s => s.Name == Job);
    }

    [Fact]
    public async Task Acquire_when_free_succeeds_and_records_start()
    {
        var acquired = await NewService().TryAcquireAsync(Job, "A", Lease);

        Assert.True(acquired);
        var state = await ReadStateAsync();
        Assert.Equal("A", state.LeaseOwner);
        Assert.Equal(_time.GetUtcNow().UtcDateTime.Add(Lease), state.LeaseExpiresAtUtc);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, state.LastStartedAtUtc);
    }

    [Fact]
    public async Task Second_owner_cannot_acquire_while_lease_is_active()
    {
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));

        _time.Advance(TimeSpan.FromMinutes(4));

        Assert.False(await NewService().TryAcquireAsync(Job, "B", Lease));
        Assert.Equal("A", (await ReadStateAsync()).LeaseOwner);
    }

    [Fact]
    public async Task Second_owner_can_acquire_after_lease_expires()
    {
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));

        _time.Advance(TimeSpan.FromMinutes(6));

        Assert.True(await NewService().TryAcquireAsync(Job, "B", Lease));
        Assert.Equal("B", (await ReadStateAsync()).LeaseOwner);
    }

    [Fact]
    public async Task Same_owner_can_reacquire_its_own_lock()
    {
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));
    }

    [Fact]
    public async Task Different_jobs_do_not_block_each_other()
    {
        Assert.True(await NewService().TryAcquireAsync(SyncJobNames.Catalog, "A", Lease));
        Assert.True(await NewService().TryAcquireAsync(SyncJobNames.LivePrices, "B", Lease));
    }

    [Fact]
    public async Task Heartbeat_extends_the_lease_for_the_owner()
    {
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));

        _time.Advance(TimeSpan.FromMinutes(4));
        Assert.True(await NewService().HeartbeatAsync(Job, "A", Lease)); // ahora caduca en +9 min

        _time.Advance(TimeSpan.FromMinutes(2)); // +6 min: sin latido ya habría caducado
        Assert.False(await NewService().TryAcquireAsync(Job, "B", Lease));
    }

    [Fact]
    public async Task Heartbeat_by_non_owner_returns_false_and_changes_nothing()
    {
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));
        var before = await ReadStateAsync();

        Assert.False(await NewService().HeartbeatAsync(Job, "B", Lease));

        var after = await ReadStateAsync();
        Assert.Equal(before.LeaseExpiresAtUtc, after.LeaseExpiresAtUtc);
        Assert.Equal("A", after.LeaseOwner);
    }

    [Fact]
    public async Task Release_success_frees_the_lock_and_records_the_result()
    {
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));
        _time.Advance(TimeSpan.FromSeconds(30));

        Assert.True(await NewService().ReleaseAsync(Job, "A", itemsProcessed: 498, itemsFailed: 2));

        var state = await ReadStateAsync();
        Assert.Null(state.LeaseOwner);
        Assert.Null(state.LeaseExpiresAtUtc);
        Assert.Equal(498, state.LastItemsProcessed);
        Assert.Equal(2, state.LastItemsFailed);
        Assert.Null(state.LastError);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, state.LastFinishedAtUtc);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, state.LastSucceededAtUtc);

        // Libre: otro puede tomarlo al instante, sin esperar a que caduque.
        Assert.True(await NewService().TryAcquireAsync(Job, "B", Lease));
    }

    [Fact]
    public async Task Release_with_error_records_failure_and_keeps_previous_success()
    {
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));
        Assert.True(await NewService().ReleaseAsync(Job, "A", 10, 0));
        var firstSuccess = (await ReadStateAsync()).LastSucceededAtUtc;

        _time.Advance(TimeSpan.FromHours(1));
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));
        Assert.True(await NewService().ReleaseAsync(Job, "A", 0, 500, error: "Proveedor caído"));

        var state = await ReadStateAsync();
        Assert.Equal("Proveedor caído", state.LastError);
        Assert.Equal(500, state.LastItemsFailed);
        Assert.Equal(firstSuccess, state.LastSucceededAtUtc); // el último éxito no se pisa
        Assert.Equal(_time.GetUtcNow().UtcDateTime, state.LastFinishedAtUtc);
        Assert.Null(state.LeaseOwner);
    }

    [Fact]
    public async Task Release_truncates_very_long_errors()
    {
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));

        await NewService().ReleaseAsync(Job, "A", 0, 1, error: new string('x', 5000));

        Assert.Equal(2000, (await ReadStateAsync()).LastError!.Length);
    }

    [Fact]
    public async Task Release_by_non_owner_does_not_free_the_lock()
    {
        Assert.True(await NewService().TryAcquireAsync(Job, "A", Lease));

        Assert.False(await NewService().ReleaseAsync(Job, "B", 1, 0));

        Assert.Equal("A", (await ReadStateAsync()).LeaseOwner);
        Assert.False(await NewService().TryAcquireAsync(Job, "B", Lease));
    }
}
