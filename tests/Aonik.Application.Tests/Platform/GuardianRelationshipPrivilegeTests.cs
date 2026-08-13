using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Party;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Caching;
using Aonik.SharedKernel.Primitives;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Storage;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 095 §7.2. <c>Guardian</c> carries legal authority over a child, and every other code in
/// <see cref="PartyRelationshipTypes"/> merely describes — so the generic relationship API, which
/// validates set membership and nothing else, must refuse it.
///
/// <para>
/// This is not a theoretical boundary. <c>OrderService</c> and <c>PayoutBeneficiaryService</c> both
/// pass a <em>caller-supplied</em> <c>RelationshipTypeCode</c> straight into
/// <c>CreateRelationshipAsync</c>. Without the refusal, an ordinary order-creation request carrying
/// <c>"Guardian"</c> would mint an edge that <c>IGuardianshipReader</c> later trusts for access to a
/// child's data — a privilege escalation reachable from a finance endpoint.
/// </para>
///
/// <para>
/// The tests therefore drive the service the way those callers do, rather than asserting on the
/// helper in isolation: it is the reachable path that matters.
/// </para>
/// </summary>
public class GuardianRelationshipPrivilegeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CallerUserId = Guid.NewGuid();

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 8, 13, 9, 0, 0, DateTimeKind.Utc);
    }

    private sealed class NoopAuditLogWriter : IAuditLogWriter
    {
        public Task LogAsync(
            string action, string resourceType, Guid resourceId, Guid tenantId, Guid? actorId,
            string? correlationId, string? detailsJson = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoopCacheInvalidationPublisher : ICacheInvalidationPublisher
    {
        public event Func<CacheInvalidationEvent, CancellationToken, Task>? Invalidated;

        public Task PublishAsync(CacheInvalidationEvent cacheInvalidationEvent, CancellationToken cancellationToken = default)
        {
            _ = Invalidated;
            return Task.CompletedTask;
        }
    }

    private sealed class NoopProfilePhotoStore : IProfilePhotoStore
    {
        public Task<PhotoUploadResult> UploadCustomerPhotoAsync(
            Guid tenantId, Guid partyId, string contentType, Stream fileStream,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DeleteCustomerPhotoAsync(string photoUrl, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public string GetPhotoUrl(string blobPath) => blobPath;
    }

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new PlatformDbContext(
            options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(CallerUserId),
            new TestClock());
    }

    private static PartyService CreateService(PlatformDbContext context)
        => new(
            context,
            new TestTenantProvider(TenantId),
            new TestClock(),
            new NoopAuditLogWriter(),
            new NoopCacheInvalidationPublisher(),
            new NoopProfilePhotoStore());

    private static async Task<(Guid Adult, Guid Child)> SeedTwoPartiesAsync(PlatformDbContext context)
    {
        var adult = new global::Aonik.Platform.Entities.Party.Party
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DisplayName = "A Parent",
            PartyType = "Person", Status = "Active"
        };
        var child = new global::Aonik.Platform.Entities.Party.Party
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DisplayName = "A Child",
            PartyType = "Person", Status = "Active"
        };
        context.Parties.AddRange(adult, child);
        await context.SaveChangesAsync();
        return (adult.Id, child.Id);
    }

    [Fact]
    public async Task CreateRelationshipAsync_Should_RejectGuardian_When_CalledThroughTheGenericPath()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var (adult, child) = await SeedTwoPartiesAsync(context);

        // Exactly how OrderService:765-773 calls it: a caller-supplied code, straight through.
        var act = async () => await service.CreateRelationshipAsync(
            new CreatePartyRelationshipRequest(adult, child, PartyRelationshipTypes.Guardian, null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*carries authority*");

        context.PartyRelationships.Should().BeEmpty("no edge may be created by the refused call");
    }

    [Fact]
    public async Task UpdateRelationshipAsync_Should_RejectGuardian_When_RetypingAnExistingEdge()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var (adult, child) = await SeedTwoPartiesAsync(context);

        // Create an ordinary descriptive edge, then try to promote it to an authority-carrying one.
        // Guarding create alone would leave this as the escalation path.
        var created = await service.CreateRelationshipAsync(
            new CreatePartyRelationshipRequest(adult, child, PartyRelationshipTypes.Friend, null));

        var act = async () => await service.UpdateRelationshipAsync(
            created.RelationshipId, PartyRelationshipTypes.Guardian, null, null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*carries authority*");

        var stored = await context.PartyRelationships.SingleAsync();
        stored.RelationshipTypeCode.Should().Be(PartyRelationshipTypes.Friend,
            "a refused update must not partially apply");
    }

    [Theory]
    [InlineData(PartyRelationshipTypes.Mother)]
    [InlineData(PartyRelationshipTypes.Father)]
    [InlineData(PartyRelationshipTypes.Child)]
    [InlineData(PartyRelationshipTypes.Sibling)]
    [InlineData(PartyRelationshipTypes.Friend)]
    public async Task CreateRelationshipAsync_Should_StillAllow_DescriptiveCodes(string code)
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var (adult, child) = await SeedTwoPartiesAsync(context);

        var act = async () => await service.CreateRelationshipAsync(
            new CreatePartyRelationshipRequest(adult, child, code, null));

        await act.Should().NotThrowAsync(
            "the refusal must be narrow — describing a family must keep working");
    }

    [Fact]
    public async Task EveryPrivilegedCode_Should_BeRefusedByTheGenericPath()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var (adult, child) = await SeedTwoPartiesAsync(context);

        PartyRelationshipTypes.Privileged.Should().NotBeEmpty();

        // Asserted over the SET rather than over Guardian by name, so a future authority-carrying
        // code inherits the protection instead of re-learning why it is needed.
        foreach (var code in PartyRelationshipTypes.Privileged)
        {
            var act = async () => await service.CreateRelationshipAsync(
                new CreatePartyRelationshipRequest(adult, child, code, null));

            await act.Should().ThrowAsync<InvalidOperationException>(
                $"'{code}' is privileged and must not be creatable through the generic API");
        }
    }

    [Fact]
    public void KinshipCodes_Should_NotBeTreatedAsGuardianship()
    {
        var child = Guid.NewGuid();

        // Parental authority is not parenthood. Foster and kinship carers, step-parents with a
        // responsibility agreement, and biological parents who do NOT hold it all break the
        // inference — and it breaks in the direction of granting access to the wrong person.
        var mother = new PartyRelationship
        {
            FromPartyId = Guid.NewGuid(), ToPartyId = child,
            RelationshipTypeCode = PartyRelationshipTypes.Mother, IsActive = true
        };

        GuardianRelationship.GrantsAuthorityOver(mother, child).Should().BeFalse();
    }

    [Fact]
    public void InactiveGuardianEdge_Should_NotGrantAuthority()
    {
        var child = Guid.NewGuid();
        var revoked = new PartyRelationship
        {
            FromPartyId = Guid.NewGuid(), ToPartyId = child,
            RelationshipTypeCode = PartyRelationshipTypes.Guardian, IsActive = false
        };

        GuardianRelationship.GrantsAuthorityOver(revoked, child).Should().BeFalse(
            "revocation and the majority transition both work by deactivating the edge");
    }

    [Fact]
    public void GuardianEdge_Should_NotGrantAuthorityInReverse()
    {
        var guardian = Guid.NewGuid();
        var child = Guid.NewGuid();
        var edge = new PartyRelationship
        {
            FromPartyId = guardian, ToPartyId = child,
            RelationshipTypeCode = PartyRelationshipTypes.Guardian, IsActive = true
        };

        GuardianRelationship.GrantsAuthorityOver(edge, child).Should().BeTrue();
        GuardianRelationship.GrantsAuthorityOver(edge, guardian).Should().BeFalse(
            "direction is from guardian to child and is never inferred in reverse");
    }
}
