using Aonik.IntegrationTests.Support;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Consent;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Database.Tests;

/// <summary>
/// Spec 095 §10.2 — <c>ConsentService.GrantAsync</c> against a real engine.
///
/// <para>
/// The unit tests prove the intent on InMemory, which enforces neither the filtered unique index nor
/// transactions. The specific defect they cannot see: the revoke and the insert are two rows of the
/// <em>same</em> entity type in one <c>SaveChanges</c>, and EF Core has no dependency to order them
/// by. If the INSERT is sent first, both rows are momentarily unrevoked and the single-active-grant
/// index rejects the whole batch — a failure that appears only against SQL Server, and only on the
/// second consent a family ever gives.
/// </para>
/// </summary>
public class ConsentServiceSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;
    private static readonly DateTime Now = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    public ConsentServiceSqlServerTests(SqlLocalDbFixture db)
    {
        _db = db;
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private sealed class NoMandateReader : IGuardianMandateReader
    {
        public Task<GuardianMandateInfo?> GetActiveMandateAsync(
            Guid tenantId, Guid partyId, CancellationToken cancellationToken = default)
            => Task.FromResult<GuardianMandateInfo?>(
                new GuardianMandateInfo(Guid.NewGuid(), Now.AddMonths(-3), "Stripe"));
    }

    private sealed class NoopRecorder : IGuardianVerificationRecorder
    {
        public Task RecordAsync(Guid g, Guid a, GuardianVerificationResult r, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<int> CountRecentFailuresAsync(Guid g, DateTime since, CancellationToken ct = default)
            => Task.FromResult(0);
    }

    private PlatformDbContext CreateContext(Guid tenantId)
        => new(
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseSqlServer(_db.ConnectionString)
                .Options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static ConsentService CreateService(PlatformDbContext context, Guid tenantId)
    {
        var clock = new TestClock();
        var factory = new GuardianVerifierFactory(new[]
        {
            (IGuardianVerifier)new PaymentInstrumentGuardianVerifier(
                new NoMandateReader(), NullLogger<PaymentInstrumentGuardianVerifier>.Instance)
        });

        return new ConsentService(
            context,
            new TestTenantProvider(tenantId),
            clock,
            new ConsentJurisdictionResolver(Microsoft.Extensions.Options.Options.Create(new ConsentOptions())),
            factory,
            new NoopRecorder(),
            new GuardianshipReader(context, clock));
    }

    [SkippableFact]
    public async Task GrantAsync_Should_SupersedeThePriorVersion_OnARealEngine()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var service = CreateService(context, tenantId);

        var guardian = await SeedPartyAsync(context, tenantId, "A Parent");
        var child = (await service.EnrolChildAsync(new EnrolChildRequest(
            guardian, "A Child", new DateOnly(2018, 6, 15), "GB", "v1", []))).ChildPartyId;

        // The assertion that matters: this must not throw. If EF sends the INSERT before the UPDATE,
        // the filtered unique index rejects the batch and every second consent in production fails.
        var supersede = async () => await service.GrantAsync(new GrantConsentRequest(
            child, guardian, ConsentPurposes.ServiceCore, "v2", "GB",
            ConsentVerificationMethods.PaymentInstrument, "ref"));

        await supersede.Should().NotThrowAsync(
            "revoke and insert share one SaveChanges, and the engine enforces one active grant");

        var grants = await context.ConsentGrants
            .AsNoTracking()
            .Where(g => g.SubjectPartyId == child && g.Purpose == ConsentPurposes.ServiceCore)
            .ToListAsync();

        grants.Should().HaveCount(2, "the superseded grant is retained as history");
        grants.Count(g => g.RevokedAt == null).Should().Be(1);
        grants.Single(g => g.RevokedAt == null).TermsVersion.Should().Be("v2");
    }

    [SkippableFact]
    public async Task EnrolChild_Should_LeaveNothingBehind_When_TheTransactionRollsBack()
    {
        RequireSqlServer();

        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(tenantId);
        var service = CreateService(context, tenantId);

        var guardian = await SeedPartyAsync(context, tenantId, "A Parent");

        // Force the enrolment to fail after the party and edge are staged, by pre-inserting a grant
        // that collides with the one enrolment will add. The transaction must roll back the child
        // and the guardian edge with it — a half-enrolled child with no consent would be exactly the
        // ungated state §12.2 exists to prevent.
        var collidingChild = await SeedPartyAsync(context, tenantId, "Existing Child");
        _ = collidingChild;

        var before = await context.Parties.AsNoTracking().CountAsync();

        var act = async () => await service.EnrolChildAsync(new EnrolChildRequest(
            guardian, "A Child", new DateOnly(2018, 6, 15), "GB",
            new string('v', 64), // TermsVersion is HasMaxLength(32) — the insert will fail
            []));

        await act.Should().ThrowAsync<Exception>();

        (await context.Parties.AsNoTracking().CountAsync()).Should().Be(before,
            "a failed enrolment must leave no orphan child party");
        (await context.PartyRelationships.AsNoTracking()
            .CountAsync(r => r.RelationshipTypeCode == PartyRelationshipTypes.Guardian))
            .Should().Be(0, "and no orphan guardian edge");
    }

    private static async Task<Guid> SeedPartyAsync(PlatformDbContext context, Guid tenantId, string name)
    {
        var party = new Aonik.Platform.Entities.Party.Party
        {
            Id = Guid.NewGuid(), TenantId = tenantId, DisplayName = name,
            PartyType = "Person", Status = "Active"
        };
        context.Parties.Add(party);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return party.Id;
    }

    private void RequireSqlServer()
        => Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
}
