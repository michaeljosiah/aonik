using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Consent;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 095 §12.1 and §12.3 — the enforcement point.
///
/// <para>
/// The gate never returns a boolean. Every test here is therefore about what it <em>refuses</em>,
/// because a gate that can be ignored is not a control — the same lesson Spec 089 §8.1 learned about
/// read/write access and Spec 032 learned about tool approval.
/// </para>
/// </summary>
public class ConsentGateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private static PlatformDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(Guid.NewGuid()),
            new TestClock());

    private static ConsentGate CreateGate(PlatformDbContext context)
    {
        var clock = new TestClock();
        return new ConsentGate(
            new ConsentReader(context, clock),
            new GuardianshipReader(context, clock),
            new TestTenantProvider(TenantId));
    }

    private static Guid SeedChild(PlatformDbContext context)
    {
        var child = new global::Aonik.Platform.Entities.Party.Party
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DisplayName = "A Child",
            PartyType = "Person", Status = "Active"
        };
        context.Parties.Add(child);
        context.SaveChanges();
        return child.Id;
    }

    private static void SeedGrant(
        PlatformDbContext context, Guid subject, string purpose,
        DateTime? expiresAt = null, DateTime? revokedAt = null)
    {
        context.ConsentGrants.Add(new ConsentGrant
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            SubjectPartyId = subject, GrantedByPartyId = Guid.NewGuid(),
            Purpose = purpose, TermsVersion = "v1", Jurisdiction = "GB",
            VerificationMethod = ConsentVerificationMethods.PaymentInstrument,
            VerifiedAt = Now.AddDays(-1), GrantedAt = Now.AddDays(-1),
            ExpiresAt = expiresAt, RevokedAt = revokedAt
        });
        context.SaveChanges();
    }

    // ── Fail closed, on every purpose ────────────────────────────────────

    [Theory]
    [InlineData(ConsentPurposes.ServiceCore)]
    [InlineData(ConsentPurposes.GenerationDisclosure)]
    [InlineData(ConsentPurposes.SafetyClassification)]
    [InlineData(ConsentPurposes.SharingExternal)]
    [InlineData(ConsentPurposes.Voice)]
    [InlineData(ConsentPurposes.Improvement)]
    [InlineData(ConsentPurposes.Marketing)]
    public async Task Ensure_Should_Refuse_EveryPurpose_WithNoGrant(string purpose)
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);

        var act = async () => await CreateGate(context).EnsureAsync(child, purpose);

        await act.Should().ThrowAsync<ConsentRequiredException>(
            $"'{purpose}' must fail closed — never 'log and continue', never a warning banner");
    }

    [Fact]
    public async Task Ensure_Should_Proceed_WhenAnActiveGrantExists()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        SeedGrant(context, child, ConsentPurposes.Voice);

        var act = async () => await CreateGate(context).EnsureAsync(child, ConsentPurposes.Voice);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Ensure_Should_Refuse_ARevokedGrant()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        SeedGrant(context, child, ConsentPurposes.Voice, revokedAt: Now.AddMinutes(-1));

        var act = async () => await CreateGate(context).EnsureAsync(child, ConsentPurposes.Voice);

        await act.Should().ThrowAsync<ConsentRequiredException>();
    }

    [Fact]
    public async Task Ensure_Should_Refuse_AnExpiredGrant()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        SeedGrant(context, child, ConsentPurposes.Voice, expiresAt: Now.AddMinutes(-1));

        var act = async () => await CreateGate(context).EnsureAsync(child, ConsentPurposes.Voice);

        await act.Should().ThrowAsync<ConsentRequiredException>();
    }

    [Fact]
    public async Task Ensure_Should_Refuse_AnEmptySubject()
    {
        await using var context = CreateDbContext();

        // A caller bug, and the safe reading of a bug is refusal. Treating an empty subject as
        // "no subject, therefore no restriction" is how a gate becomes a no-op under a null.
        var act = async () => await CreateGate(context)
            .EnsureAsync(Guid.Empty, ConsentPurposes.ServiceCore);

        await act.Should().ThrowAsync<ConsentRequiredException>();
    }

    [Fact]
    public async Task Ensure_Should_NotLeakAcrossSubjects()
    {
        await using var context = CreateDbContext();
        var sibling = SeedChild(context);
        var child = SeedChild(context);
        SeedGrant(context, sibling, ConsentPurposes.Voice);

        var act = async () => await CreateGate(context).EnsureAsync(child, ConsentPurposes.Voice);

        await act.Should().ThrowAsync<ConsentRequiredException>(
            "one child's consent says nothing about another's, even within one family");
    }

    // ── Route resolution (§12.3) ─────────────────────────────────────────

    [Fact]
    public async Task Generation_Should_Refuse_RemoteRoute_WithoutDisclosureConsent()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        SeedGrant(context, child, ConsentPurposes.ServiceCore);
        SeedGrant(context, child, ConsentPurposes.SafetyClassification);

        var act = async () => await CreateGate(context)
            .EnsureGenerationAsync(child, GenerationRoute.Remote);

        await act.Should().ThrowAsync<ConsentRequiredException>()
            .Where(e => e.Purpose == ConsentPurposes.GenerationDisclosure);
    }

    [Fact]
    public async Task Generation_Should_Allow_LocalRoute_WithoutDisclosureConsent()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        SeedGrant(context, child, ConsentPurposes.ServiceCore);
        SeedGrant(context, child, ConsentPurposes.SafetyClassification);

        // The family declined remote authoring. Refusing them the local route as well would deny
        // them the privacy-preserving option they were declining in favour of — the contradiction
        // §12.3 exists to fix.
        var act = async () => await CreateGate(context)
            .EnsureGenerationAsync(child, GenerationRoute.LocalWithRemoteClassification);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Generation_Should_StillRequireClassificationConsent_OnTheLocalRoute()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        SeedGrant(context, child, ConsentPurposes.ServiceCore);

        // "Nothing leaves the device" was too quick: Spec 096 requires output classification on every
        // child-facing generation, and its classifiers are external. Locally authored content is
        // still SENT — to be judged rather than written, but sent.
        var act = async () => await CreateGate(context)
            .EnsureGenerationAsync(child, GenerationRoute.LocalWithRemoteClassification);

        await act.Should().ThrowAsync<ConsentRequiredException>()
            .Where(e => e.Purpose == ConsentPurposes.SafetyClassification);
    }

    [Fact]
    public async Task Generation_Should_RequireOnlyServiceCore_OnTheFullyLocalRoute()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        SeedGrant(context, child, ConsentPurposes.ServiceCore);

        // Unreachable today — it needs on-device classifiers, which do not exist. Encoded so the
        // shape is right when they do, rather than implying a capability we have.
        var act = async () => await CreateGate(context)
            .EnsureGenerationAsync(child, GenerationRoute.FullyLocal);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Generation_Should_RequireServiceCore_OnEveryRoute()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        SeedGrant(context, child, ConsentPurposes.GenerationDisclosure);
        SeedGrant(context, child, ConsentPurposes.SafetyClassification);

        var act = async () => await CreateGate(context)
            .EnsureGenerationAsync(child, GenerationRoute.Remote);

        await act.Should().ThrowAsync<ConsentRequiredException>()
            .Where(e => e.Purpose == ConsentPurposes.ServiceCore,
                "service-core is what makes the account exist at all");
    }

    [Theory]
    [InlineData(true, true, 2)]
    [InlineData(false, true, 1)]
    [InlineData(false, false, 0)]
    public void Route_Should_DeclareItsPurposes(bool remote, bool classifies, int expected)
    {
        new GenerationRoute(remote, classifies).RequiredPurposes().Should().HaveCount(expected);
    }

    // ── Acting for another party ─────────────────────────────────────────

    [Fact]
    public async Task ActFor_Should_Refuse_APartyWithNoGuardianEdge()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        var stranger = SeedChild(context);

        var act = async () => await CreateGate(context).EnsureCanActForAsync(stranger, child);

        await act.Should().ThrowAsync<GuardianAuthorityRequiredException>();
    }

    [Fact]
    public async Task ActFor_Should_Allow_AGuardian()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        var guardian = SeedChild(context);

        context.PartyRelationships.Add(new PartyRelationship
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FromPartyId = guardian, ToPartyId = child,
            RelationshipTypeCode = PartyRelationshipTypes.Guardian, IsActive = true
        });
        await context.SaveChangesAsync();

        var act = async () => await CreateGate(context).EnsureCanActForAsync(guardian, child);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ActFor_Should_AlwaysAllowActingForOneself()
    {
        await using var context = CreateDbContext();
        var subject = SeedChild(context);

        // This is what makes the age-up transition work: at ConsentAgeOn the guardian edge is still
        // active, but the young person is the one deciding.
        var act = async () => await CreateGate(context).EnsureCanActForAsync(subject, subject);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ActFor_Should_Refuse_AKinshipEdge()
    {
        await using var context = CreateDbContext();
        var child = SeedChild(context);
        var mother = SeedChild(context);

        context.PartyRelationships.Add(new PartyRelationship
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            FromPartyId = mother, ToPartyId = child,
            RelationshipTypeCode = PartyRelationshipTypes.Mother, IsActive = true
        });
        await context.SaveChangesAsync();

        // Parental authority is not parenthood. Inferring it gets real families wrong in the
        // direction of granting access to someone who should not have it.
        var act = async () => await CreateGate(context).EnsureCanActForAsync(mother, child);

        await act.Should().ThrowAsync<GuardianAuthorityRequiredException>();
    }
}
