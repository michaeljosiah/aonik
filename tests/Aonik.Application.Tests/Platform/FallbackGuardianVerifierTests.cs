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

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 095 §8 fallbacks — for guardians on a £0 tier with no payment instrument.
///
/// <para>
/// The most important test in this file asserts that government-ID verification is
/// <strong>off by default</strong>. <c>ComplianceService.ScreenPartyAsync</c> is a stub that always
/// returns Passed, so enabling it today would verify every guardian automatically and write consent
/// records citing <c>government-id</c> — evidence, in an audit, of a check that never happened.
/// </para>
/// </summary>
public class FallbackGuardianVerifierTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid StaffUserId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 8, 13, 12, 0, 0, DateTimeKind.Utc);

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; } = Now;
    }

    private sealed class StubComplianceService : IComplianceService
    {
        private readonly string _status;
        public int Calls { get; private set; }

        public StubComplianceService(string status = "Passed") => _status = status;

        public Task<ScreeningResult> ScreenPartyAsync(
            Guid partyId, string checkType, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ScreeningResult(
                Guid.NewGuid(), partyId, checkType, _status, "Approved", Now));
        }

        public Task<ComplianceCaseResponse> CreateOrderReviewCaseAsync(
            Guid orderId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> RequiresComplianceReviewAsync(
            Guid orderId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class NoopAuditLogWriter : IAuditLogWriter
    {
        public Task LogAsync(
            string action, string resourceType, Guid resourceId, Guid tenantId, Guid? actorId,
            string? correlationId, string? detailsJson = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static PlatformDbContext CreateDbContext()
        => new(
            new DbContextOptionsBuilder<PlatformDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options,
            new TestTenantProvider(TenantId),
            new TestCurrentUserProvider(StaffUserId),
            new TestClock());

    private static GovernmentIdGuardianVerifier CreateIdVerifier(
        StubComplianceService compliance, bool enabled)
        => new(
            compliance,
            Microsoft.Extensions.Options.Options.Create(new ConsentOptions
            {
                GovernmentIdVerification = new GovernmentIdVerificationOptions { Enabled = enabled }
            }),
            NullLogger<GovernmentIdGuardianVerifier>.Instance);

    private static SignedFormGuardianVerifier CreateFormVerifier(PlatformDbContext context)
        => new(context, new TestTenantProvider(TenantId), new TestClock());

    private static GuardianAttestationService CreateAttestationService(
        PlatformDbContext context, int expiryDays = 365)
        => new(
            context,
            new TestTenantProvider(TenantId),
            new TestClock(),
            new NoopAuditLogWriter(),
            Microsoft.Extensions.Options.Options.Create(new ConsentOptions
            {
                SignedFormAttestationDays = expiryDays
            }));

    private static Guid SeedParty(PlatformDbContext context)
    {
        var party = new global::Aonik.Platform.Entities.Party.Party
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DisplayName = "A Carer",
            PartyType = "Person", Status = "Active"
        };
        context.Parties.Add(party);
        context.SaveChanges();
        return party.Id;
    }

    // ── Government ID: off by default, and that is the point ─────────────

    [Fact]
    public void GovernmentIdVerification_Should_BeDisabledByDefault()
    {
        // The single most important assertion here. ScreenPartyAsync always returns Passed, so a
        // default of enabled would verify every guardian who asked — and produce a consent record
        // citing government-id, which is evidence of a check that never happened.
        new ConsentOptions().GovernmentIdVerification.Enabled.Should().BeFalse(
            "no document-verification provider exists; enabling this against the stub is worse "
            + "than not offering the method at all");
    }

    [Fact]
    public async Task GovernmentId_Should_ReportUnavailable_WhenDisabled()
    {
        var verifier = CreateIdVerifier(new StubComplianceService(), enabled: false);

        (await verifier.IsAvailableAsync(TenantId, Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task GovernmentId_Should_FailClosed_AndNotCallScreening_WhenDisabled()
    {
        var compliance = new StubComplianceService();
        var verifier = CreateIdVerifier(compliance, enabled: false);

        var result = await verifier.VerifyAsync(TenantId, Guid.NewGuid());

        result.Succeeded.Should().BeFalse(
            "a caller reaching Verify despite IsAvailable saying no is a bug, and the safe reading "
            + "of a bug is refusal");
        compliance.Calls.Should().Be(0, "and the stub must not even be consulted");
    }

    [Fact]
    public async Task GovernmentId_Should_Verify_WhenEnabledAndTheCheckPasses()
    {
        var verifier = CreateIdVerifier(new StubComplianceService("Passed"), enabled: true);

        var result = await verifier.VerifyAsync(TenantId, Guid.NewGuid());

        result.Succeeded.Should().BeTrue();
        result.OutcomeRef.Should().NotBeNullOrWhiteSpace(
            "the record cites the check, never the document");
    }

    [Fact]
    public async Task GovernmentId_Should_Fail_WhenTheCheckDoesNotPass()
    {
        var verifier = CreateIdVerifier(new StubComplianceService("Referred"), enabled: true);

        (await verifier.VerifyAsync(TenantId, Guid.NewGuid())).Succeeded.Should().BeFalse();
    }

    // ── Signed form: the platform holds evidence, it does not check ──────

    [Fact]
    public async Task SignedForm_Should_ReportUnavailable_WithNoAttestation()
    {
        await using var context = CreateDbContext();
        var carer = SeedParty(context);

        (await CreateFormVerifier(context).IsAvailableAsync(TenantId, carer)).Should().BeFalse();
    }

    [Fact]
    public async Task SignedForm_Should_Verify_AfterAnOperatorAttests()
    {
        await using var context = CreateDbContext();
        var carer = SeedParty(context);

        // This is what makes the £0-tier path work: no card, and a human did the verification.
        await CreateAttestationService(context).AttestAsync(carer, StaffUserId, "CASE-1234", null);

        var result = await CreateFormVerifier(context).VerifyAsync(TenantId, carer);

        result.Succeeded.Should().BeTrue();
        result.Method.Should().Be(ConsentVerificationMethods.SignedForm);
    }

    [Fact]
    public async Task Attest_Should_Refuse_WithoutANamedStaffMember()
    {
        await using var context = CreateDbContext();
        var carer = SeedParty(context);

        // "Someone in support checked it" is not an attestation. This argument is what makes it one.
        var act = async () => await CreateAttestationService(context)
            .AttestAsync(carer, Guid.Empty, null, null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*name the staff member*");
    }

    [Fact]
    public async Task SignedForm_Should_StopVerifying_OnceTheAttestationExpires()
    {
        await using var context = CreateDbContext();
        var carer = SeedParty(context);

        // Expired the moment it was written. An attestation is a statement about a MOMENT, and
        // treating an old one as current is how a manual process quietly becomes no process.
        await CreateAttestationService(context, expiryDays: 0).AttestAsync(carer, StaffUserId, null, null);

        (await CreateFormVerifier(context).VerifyAsync(TenantId, carer)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task SignedForm_Should_StopVerifying_OnceRevoked()
    {
        await using var context = CreateDbContext();
        var carer = SeedParty(context);
        var service = CreateAttestationService(context);

        var id = await service.AttestAsync(carer, StaffUserId, "CASE-1234", null);
        await service.RevokeAsync(id, "Form found to be forged.");

        (await CreateFormVerifier(context).VerifyAsync(TenantId, carer)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task RevokingAnAttestation_Should_NotRetroactivelyRevokeConsent()
    {
        await using var context = CreateDbContext();
        var carer = SeedParty(context);
        var child = SeedParty(context);
        var service = CreateAttestationService(context);

        var id = await service.AttestAsync(carer, StaffUserId, "CASE-1234", null);

        context.ConsentGrants.Add(new ConsentGrant
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            SubjectPartyId = child, GrantedByPartyId = carer,
            Purpose = ConsentPurposes.ServiceCore, TermsVersion = "v1", Jurisdiction = "GB",
            VerificationMethod = ConsentVerificationMethods.SignedForm,
            VerifiedAt = Now, GrantedAt = Now
        });
        await context.SaveChangesAsync();

        await service.RevokeAsync(id, "Arrangement ended.");

        // Lawfulness is judged at the TIME of processing. The grant stands; what changes is that the
        // attestation no longer supports new ones.
        (await context.ConsentGrants.SingleAsync()).RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Attest_Should_Refuse_AnUnknownParty()
    {
        await using var context = CreateDbContext();

        var act = async () => await CreateAttestationService(context)
            .AttestAsync(Guid.NewGuid(), StaffUserId, null, null);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── The £0-tier path end to end ──────────────────────────────────────

    [Fact]
    public async Task Factory_Should_ResolveSignedForm_ForAGuardianWithNoCard()
    {
        await using var context = CreateDbContext();
        var carer = SeedParty(context);
        await CreateAttestationService(context).AttestAsync(carer, StaffUserId, "CASE-1234", null);

        var factory = new GuardianVerifierFactory(new IGuardianVerifier[]
        {
            new PaymentInstrumentGuardianVerifier(
                new NoMandateReader(), NullLogger<PaymentInstrumentGuardianVerifier>.Instance),
            CreateIdVerifier(new StubComplianceService(), enabled: false),
            CreateFormVerifier(context),
        });

        var jurisdiction = new ConsentJurisdictionResolver(
            Microsoft.Extensions.Options.Options.Create(new ConsentOptions())).Resolve("GB");

        var resolved = await factory.ResolveAsync(TenantId, carer, jurisdiction);

        resolved.Should().NotBeNull();
        resolved!.Method.Should().Be(ConsentVerificationMethods.SignedForm,
            "the strongest AVAILABLE method — no card, and ID verification is switched off");
    }

    private sealed class NoMandateReader : IGuardianMandateReader
    {
        public Task<GuardianMandateInfo?> GetActiveMandateAsync(
            Guid tenantId, Guid partyId, CancellationToken cancellationToken = default)
            => Task.FromResult<GuardianMandateInfo?>(null);
    }
}
