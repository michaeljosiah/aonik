using Aonik.Platform.Entities.Party;
using Aonik.Platform.Services.Consent;
using Aonik.SharedKernel.Abstractions.Consent;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Platform;

/// <summary>
/// Spec 095 G2. Two things carry the weight here: the strict default for an unmapped jurisdiction,
/// and the absence of any "unverified" fallback when no method is available.
///
/// <para>
/// Both are wrong-way-default risks. A permissive answer in either place does not fail loudly — it
/// quietly processes a child's data under a threshold nobody checked, or on a consent nobody
/// evidenced, and looks like a working system while doing it.
/// </para>
/// </summary>
public class GuardianVerificationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static ConsentJurisdictionResolver CreateResolver(ConsentOptions? options = null)
        => new(Microsoft.Extensions.Options.Options.Create(options ?? new ConsentOptions()));

    private sealed class StubMandateReader : IGuardianMandateReader
    {
        private readonly GuardianMandateInfo? _mandate;
        public StubMandateReader(GuardianMandateInfo? mandate = null) => _mandate = mandate;

        public Task<GuardianMandateInfo?> GetActiveMandateAsync(
            Guid tenantId, Guid partyId, CancellationToken cancellationToken = default)
            => Task.FromResult(_mandate);
    }

    private static PaymentInstrumentGuardianVerifier CreateVerifier(GuardianMandateInfo? mandate)
        => new(new StubMandateReader(mandate), NullLogger<PaymentInstrumentGuardianVerifier>.Instance);

    private static GuardianMandateInfo AMandate()
        => new(Guid.NewGuid(), new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), "Stripe");

    // ── Jurisdiction resolution ──────────────────────────────────────────

    [Fact]
    public void Resolve_Should_ReturnTheStatutoryRules_For_AKnownJurisdiction()
    {
        var jurisdiction = CreateResolver().Resolve("GB");

        jurisdiction.ConsentAge.Should().Be(13);
        jurisdiction.MajorityAge.Should().Be(18, "guardianship outlives the consent threshold");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ZZ")]
    [InlineData("Atlantis")]
    public void Resolve_Should_DefaultStrict_For_AnUnmappedJurisdiction(string? code)
    {
        // The wrong-way default here is a breach discovered through a regulator. Being over-strict
        // costs a support conversation.
        var jurisdiction = CreateResolver().Resolve(code);

        jurisdiction.ConsentAge.Should().Be(16, "an unknown country takes the GDPR Article 8 default");
        jurisdiction.AcceptedMethods.Should().NotContain(ConsentVerificationMethods.SignedForm,
            "the strict default admits only the strongest methods");
    }

    [Fact]
    public void Resolve_Should_NeverOfferSelfAuthenticated_AsAParentalMethod()
    {
        // self-authenticated is a real verification, but only of the subject BY the subject. It can
        // never be used to consent on someone else's behalf (Spec 095 §11.3).
        foreach (var code in new[] { "GB", "ZZ" })
        {
            CreateResolver().Resolve(code).AcceptedMethods
                .Should().NotContain(ConsentVerificationMethods.SelfAuthenticated,
                    $"'{code}' must not accept a self-grant as parental verification");
        }
    }

    [Fact]
    public void Resolve_Should_AllowATenantToRaiseTheThreshold()
    {
        var options = new ConsentOptions
        {
            Jurisdictions = { new ConsentJurisdictionOptions { Code = "GB", ConsentAge = 16, MajorityAge = 18 } }
        };

        CreateResolver(options).Resolve("GB").ConsentAge.Should().Be(16,
            "an operator may be stricter than the law");
    }

    [Fact]
    public void Resolve_Should_IgnoreATenantAttemptToLowerTheThreshold()
    {
        var options = new ConsentOptions
        {
            Jurisdictions = { new ConsentJurisdictionOptions { Code = "GB", ConsentAge = 8, MajorityAge = 16 } }
        };

        var jurisdiction = CreateResolver(options).Resolve("GB");

        jurisdiction.ConsentAge.Should().Be(13, "configuration may raise the statutory age, never lower it");
        jurisdiction.MajorityAge.Should().Be(18);
    }

    // ── Payment-instrument verification ──────────────────────────────────

    [Fact]
    public async Task PaymentInstrument_Should_Verify_When_ThePartyHoldsAnActiveMandate()
    {
        var mandate = AMandate();
        var result = await CreateVerifier(mandate).VerifyAsync(TenantId, Guid.NewGuid());

        result.Succeeded.Should().BeTrue(
            "the subscribing parent's mandate makes the strongest method a by-product of paying");
        result.Method.Should().Be(ConsentVerificationMethods.PaymentInstrument);
        result.OutcomeRef.Should().Be(mandate.MandateId.ToString(),
            "the record cites the authorisation, never the instrument");
    }

    [Fact]
    public async Task PaymentInstrument_Should_FailCleanly_When_ThereIsNoMandate()
    {
        var result = await CreateVerifier(mandate: null).VerifyAsync(TenantId, Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrWhiteSpace(
            "a failure is an outcome to record, not an exception to swallow");
    }

    [Fact]
    public async Task PaymentInstrument_Should_ReportUnavailable_When_ThereIsNoMandate()
    {
        (await CreateVerifier(mandate: null).IsAvailableAsync(TenantId, Guid.NewGuid()))
            .Should().BeFalse("a £0-tier guardian needs to be offered a fallback, not a method that will fail");
    }

    // ── Factory selection ────────────────────────────────────────────────

    [Fact]
    public async Task Factory_Should_ReturnNull_When_NoAcceptedMethodIsAvailable()
    {
        // The most important assertion in this file. Returning null means the caller must not
        // proceed; anything else here would be an unverified-consent path, which is the single
        // failure this whole specification exists to prevent.
        var factory = new GuardianVerifierFactory(new[] { (IGuardianVerifier)CreateVerifier(mandate: null) });

        var resolved = await factory.ResolveAsync(TenantId, Guid.NewGuid(), CreateResolver().Resolve("GB"));

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task Factory_Should_PickTheStrongestAvailableMethod()
    {
        var factory = new GuardianVerifierFactory(new[] { (IGuardianVerifier)CreateVerifier(AMandate()) });

        var resolved = await factory.ResolveAsync(TenantId, Guid.NewGuid(), CreateResolver().Resolve("GB"));

        resolved.Should().NotBeNull();
        resolved!.Method.Should().Be(ConsentVerificationMethods.PaymentInstrument,
            "AcceptedMethods is ordered strongest-first and the factory honours that order");
    }

    [Fact]
    public async Task Factory_Should_IgnoreAMethodTheJurisdictionDoesNotAccept()
    {
        // A verifier we have implemented is not a candidate where the jurisdiction does not accept
        // it — the jurisdiction decides what counts, not the registration list.
        var signedForm = new StubVerifier(ConsentVerificationMethods.SignedForm, available: true);
        var factory = new GuardianVerifierFactory(new[] { (IGuardianVerifier)signedForm });

        var strictDefault = CreateResolver().Resolve("ZZ");
        strictDefault.AcceptedMethods.Should().NotContain(ConsentVerificationMethods.SignedForm);

        (await factory.ResolveAsync(TenantId, Guid.NewGuid(), strictDefault)).Should().BeNull();
    }

    [Fact]
    public void Factory_ForMethod_Should_ReturnNull_For_AnUnknownMethod()
    {
        var factory = new GuardianVerifierFactory(new[] { (IGuardianVerifier)CreateVerifier(AMandate()) });

        factory.ForMethod("not-a-method").Should().BeNull();
        factory.ForMethod(ConsentVerificationMethods.LegacyUnverified).Should().BeNull(
            "legacy-unverified is an archive marker and must never be resolvable as a verifier");
    }

    private sealed class StubVerifier : IGuardianVerifier
    {
        private readonly bool _available;
        public StubVerifier(string method, bool available)
        {
            Method = method;
            _available = available;
        }

        public string Method { get; }

        public Task<bool> IsAvailableAsync(Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken = default)
            => Task.FromResult(_available);

        public Task<GuardianVerificationResult> VerifyAsync(Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken = default)
            => Task.FromResult(GuardianVerificationResult.Success(Method, "stub"));
    }
}
