namespace Aonik.Platform.Contracts.Api.Consent;

// The wire shapes of the guardian-facing consent endpoints (Spec 095, aonik#326/#328). A product
// host reads these to decide, for each child, what it may do; it never decides from its own records.

public sealed record ConsentGrantResponse(
    string Purpose,
    string TermsVersion,
    string VerificationMethod,
    Guid GrantedByPartyId,
    DateTime GrantedAt,
    DateTime? ExpiresAt);

/// <param name="ConsentBand">Which side of the consent-age line the child is on (Spec 095 §5).</param>
/// <param name="SafetyBand">The generation safety band Spec 096 applies to this child.</param>
/// <param name="Purposes">The consent that stands right now: active, unexpired, unrevoked grants.</param>
public sealed record WardResponse(
    Guid ChildPartyId,
    string DisplayName,
    string? ConsentBand,
    string? SafetyBand,
    DateTime? ConsentAgeOn,
    DateTime? MajorityOn,
    IReadOnlyList<ConsentGrantResponse> Purposes);

public sealed record WardListResponse(IReadOnlyList<WardResponse> Wards);

/// <param name="DateOfBirth">Attested, exact, ISO date. Only the year is kept (Spec 095 §6).</param>
/// <param name="Jurisdiction">A country code; null takes the tenant's default, which is the strict one.</param>
/// <param name="TermsVersion">The terms the guardian is agreeing to.</param>
/// <param name="Purposes">Beyond <c>service-core</c>, which is always granted. Nothing is pre-ticked.</param>
public sealed record EnrolWardRequest(
    string DisplayName,
    DateOnly DateOfBirth,
    string TermsVersion,
    string? Jurisdiction = null,
    IReadOnlyList<string>? Purposes = null);

public sealed record EnrolWardResponse(
    Guid ChildPartyId,
    Guid EnrolmentAttemptId,
    string VerificationMethod,
    DateTime ConsentAgeOn,
    DateTime MajorityOn,
    string SafetyBand);

public sealed record GrantPurposeRequest(
    string Purpose,
    string TermsVersion,
    string? Jurisdiction = null);

public sealed record GrantPurposeResponse(string Purpose, string VerificationMethod);

public sealed record WithdrawPurposeResponse(string Purpose, bool Withdrawn);

/// <param name="GuardianPartyId">The adult being given authority. Never the caller.</param>
/// <param name="Jurisdiction">Decides which routes may verify the new guardian; null takes the tenant default.</param>
public sealed record AddWardGuardianRequest(
    Guid GuardianPartyId,
    string? Jurisdiction = null);

public sealed record AddWardGuardianResponse(Guid ChildPartyId, Guid GuardianPartyId);

/// <param name="EvidenceRef">Where the form is filed — a document id, a case number — never the form itself.</param>
public sealed record AttestGuardianRequest(
    Guid GuardianPartyId,
    string? EvidenceRef = null,
    string? Notes = null);

public sealed record AttestGuardianResponse(Guid AttestationId, Guid GuardianPartyId);

public sealed record RevokeAttestationRequest(string Reason);

/// <summary>Every refusal these endpoints make on purpose: a stable code and a message for a log.</summary>
public sealed record ConsentProblem(int Status, string Code, string Message);
