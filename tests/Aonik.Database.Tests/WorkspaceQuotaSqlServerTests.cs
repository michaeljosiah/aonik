using Aonik.IntegrationTests.Support;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using Aonik.Workspaces.Entities;
using Aonik.Workspaces.Persistence;
using Aonik.Workspaces.Services;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Database.Tests;

/// <summary>
/// Spec 089 §9 — the byte ceiling, and the widening it forced on Spec 087.
///
/// <para>
/// The overflow test is here rather than InMemory because a column-width truncation is invisible to a provider
/// with no columns. <c>int.MaxValue</c> is 2,147,483,647 and a 200GB allowance is 214,748,364,800 — a hundred
/// times larger, and even a 3GB world overflows it. A wrapped aggregate does not fail loudly; it under-counts, so
/// the ceiling stops refusing and the storage bill is found later.
/// </para>
/// </summary>
public class WorkspaceQuotaSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;
    private static readonly DateTime Now = new(2026, 8, 14, 11, 0, 0, DateTimeKind.Utc);

    public WorkspaceQuotaSqlServerTests(SqlLocalDbFixture db) => _db = db;

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    /// <summary>
    /// Records claims and releases with their weights, and enforces a ceiling of its own so the
    /// possession accounting can be observed without standing up the whole subscriptions module.
    /// </summary>
    private sealed class LedgerMeter : IUsageMeter
    {
        private readonly long _ceiling;
        private readonly Dictionary<string, long> _held = [];

        public LedgerMeter(long ceiling = long.MaxValue) => _ceiling = ceiling;

        public List<(string Meter, string Holder, long Weight)> Claims { get; } = [];
        public List<(string Meter, string Holder)> Releases { get; } = [];

        public long HeldFor(string meterCode) => _held.GetValueOrDefault(meterCode);

        public Task ClaimSlotAsync(
            SubscriberRef subscriber, string meterCode, string holderRef, long weight = 1,
            CancellationToken cancellationToken = default)
        {
            var key = $"{subscriber.Kind}:{subscriber.Id}:{meterCode}:{holderRef}";

            if (Claims.Any(c => c.Meter == key))
            {
                return Task.CompletedTask;
            }

            var current = _held.GetValueOrDefault(meterCode);

            if (current + weight > _ceiling)
            {
                throw new EntitlementExceededException(meterCode, weight, Math.Max(0, _ceiling - current));
            }

            _held[meterCode] = current + weight;
            Claims.Add((key, holderRef, weight));
            return Task.CompletedTask;
        }

        public Task ReleaseSlotAsync(
            SubscriberRef subscriber, string meterCode, string holderRef,
            CancellationToken cancellationToken = default)
        {
            var key = $"{subscriber.Kind}:{subscriber.Id}:{meterCode}:{holderRef}";
            var claim = Claims.FirstOrDefault(c => c.Meter == key);

            if (claim.Meter is not null)
            {
                Claims.Remove(claim);
                _held[meterCode] = Math.Max(0, _held.GetValueOrDefault(meterCode) - claim.Weight);
            }

            Releases.Add((meterCode, holderRef));
            return Task.CompletedTask;
        }

        public Task<UsageReservationRef> ReserveAsync(
            SubscriberRef subscriber, string meterCode, decimal quantity, string idempotencyKey,
            TimeSpan? holdFor = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UsageCommitResult> CommitAsync(
            Guid reservationId, decimal actualQuantity, UsageSource source,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> HasFlagAsync(
            SubscriberRef subscriber, string meterCode, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private WorkspacesDbContext CreateContext(Guid tenantId)
        => new(
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseSqlServer(_db.ConnectionString)
                .Options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static BlobPossessionService CreatePossessions(
        WorkspacesDbContext context, Guid tenantId, IUsageMeter meter)
        => new(context, meter, new TestTenantProvider(tenantId),
            NullLogger<BlobPossessionService>.Instance);

    private static SubscriberRef Party(Guid id) => new(SubscriberKinds.Party, id);

    private static Dictionary<string, long> Hashes(params (string Seed, long Size)[] entries)
        => entries.ToDictionary(
            e => System.Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(e.Seed))).ToLowerInvariant(),
            e => e.Size,
            StringComparer.OrdinalIgnoreCase);

    // ── The widening ─────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AByteCeiling_Should_CountBeyondIntMaxWithoutTruncating()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var meter = new LedgerMeter();

        // Three blobs of a little under 1GB each, plus one that carries the total past int.MaxValue.
        var hashes = Hashes(
            ("take-one", 900_000_000L),
            ("take-two", 900_000_000L),
            ("take-three", 900_000_000L));

        await CreatePossessions(context, tenantId, meter).AcquireAsync(subscriber, hashes);

        // 2,700,000,000 — comfortably past 2,147,483,647. An int aggregate would have wrapped to a
        // negative number here and the ceiling would have stopped refusing anything.
        meter.HeldFor(WorkspaceMeters.Bytes).Should().Be(2_700_000_000L);

        var stored = await context.Possessions.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .SumAsync(p => p.SizeBytes);

        stored.Should().Be(2_700_000_000L, "the column has to hold it as well as the arithmetic");
    }

    [SkippableFact]
    public async Task ExceedingTheByteCeiling_Should_RefuseBeforeAnyByteIsStored()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var meter = new LedgerMeter(ceiling: 1_000_000L);

        var act = async () => await CreatePossessions(context, tenantId, meter)
            .AcquireAsync(subscriber, Hashes(("huge", 2_000_000L)));

        // "You are out of space" rather than a surprise bill or a silent truncation.
        await act.Should().ThrowAsync<EntitlementExceededException>();
    }

    // ── Per-subscriber possession ────────────────────────────────────────

    [SkippableFact]
    public async Task TwoWorkspacesSharingABlob_Should_ChargeOnce()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var meter = new LedgerMeter();
        var possessions = CreatePossessions(context, tenantId, meter);
        var shared = Hashes(("reference-image", 5_000L));

        await possessions.AcquireAsync(subscriber, shared);
        await possessions.AcquireAsync(subscriber, shared);

        // Claiming is idempotent per content hash, so the same bytes referenced twice are charged once.
        meter.HeldFor(WorkspaceMeters.Bytes).Should().Be(5_000L);

        (await context.Possessions.AsNoTracking().SingleAsync()).WorkspaceCount.Should().Be(2);
    }

    [SkippableFact]
    public async Task ReleasingOneOfTwoWorkspaces_Should_KeepCharging()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var meter = new LedgerMeter();
        var possessions = CreatePossessions(context, tenantId, meter);
        var shared = Hashes(("reference-image", 5_000L));

        await possessions.AcquireAsync(subscriber, shared);
        await possessions.AcquireAsync(subscriber, shared);

        await possessions.ReleaseAsync(subscriber, [.. shared.Keys]);

        // THE §9.2 HOLE. The elegance of keying the claim on the hash is exactly what makes release
        // ambiguous: one claim covers all their workspaces that reference it, so releasing here would
        // leave the retained workspace's bytes completely uncharged while the physical blob cannot be
        // swept and we are still paying for it.
        meter.HeldFor(WorkspaceMeters.Bytes).Should().Be(5_000L);
        meter.Releases.Should().BeEmpty();

        (await context.Possessions.AsNoTracking().SingleAsync()).WorkspaceCount.Should().Be(1);
    }

    [SkippableFact]
    public async Task ReleasingTheLastWorkspace_Should_StopCharging()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var meter = new LedgerMeter();
        var possessions = CreatePossessions(context, tenantId, meter);
        var shared = Hashes(("reference-image", 5_000L));

        await possessions.AcquireAsync(subscriber, shared);
        await possessions.ReleaseAsync(subscriber, [.. shared.Keys]);

        meter.HeldFor(WorkspaceMeters.Bytes).Should().Be(0);
        (await context.Possessions.AsNoTracking().AnyAsync()).Should().BeFalse();
    }

    [SkippableFact]
    public async Task ARecipientAlreadyHoldingTheBytes_Should_OweNothingExtra()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var subscriber = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var possessions = CreatePossessions(context, tenantId, new LedgerMeter());
        var shared = Hashes(("reference-image", 5_000L));

        await possessions.AcquireAsync(subscriber, shared);

        // The same dedupe property the physical store gives, one level up.
        (await possessions.ProjectedWeightAsync(subscriber, shared)).Should().Be(0);
    }

    [SkippableFact]
    public async Task TwoSubscribersHoldingOneBlob_Should_EachBeCharged()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        var alice = Party(Guid.NewGuid());
        var bob = Party(Guid.NewGuid());
        await using var context = CreateContext(tenantId);
        var meter = new LedgerMeter();
        var possessions = CreatePossessions(context, tenantId, meter);
        var shared = Hashes(("reference-image", 5_000L));

        await possessions.AcquireAsync(alice, shared);
        await possessions.AcquireAsync(bob, shared);

        // Physical dedupe is a storage saving, not a billing one: the blob exists once and both
        // subscribers are keeping it alive.
        meter.HeldFor(WorkspaceMeters.Bytes).Should().Be(10_000L);
        (await context.Possessions.AsNoTracking().CountAsync()).Should().Be(2);
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
