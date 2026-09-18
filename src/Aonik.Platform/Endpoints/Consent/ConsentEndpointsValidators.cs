using Aonik.Platform.Contracts.Api.Consent;
using Aonik.SharedKernel.Abstractions.Consent;

using FastEndpoints;
using FluentValidation;

namespace Aonik.Platform.Endpoints.Consent;

// Structural validation of the consent request DTOs. What the platform refuses on the merits —
// verification, authority, purpose semantics — stays in the service; these say only that the
// request is well-formed enough to be asked.

internal sealed class EnrolWardRequestValidator : Validator<EnrolWardRequest>
{
    public EnrolWardRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TermsVersion).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DateOfBirth).NotEqual(default(DateOnly)).WithMessage("An attested date of birth is required.");
        RuleFor(x => x.Jurisdiction).Length(2).When(x => x.Jurisdiction is not null).WithMessage("jurisdiction is a two-letter country code.");
        RuleForEach(x => x.Purposes).Must(p => ConsentPurposes.All.Contains(p)).WithMessage("Unknown consent purpose.");
    }
}

internal sealed class GrantPurposeRequestValidator : Validator<GrantPurposeRequest>
{
    public GrantPurposeRequestValidator()
    {
        RuleFor(x => x.Purpose).NotEmpty().Must(p => ConsentPurposes.All.Contains(p)).WithMessage("Unknown consent purpose.");
        RuleFor(x => x.TermsVersion).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Jurisdiction).Length(2).When(x => x.Jurisdiction is not null).WithMessage("jurisdiction is a two-letter country code.");
    }
}

internal sealed class AttestGuardianRequestValidator : Validator<AttestGuardianRequest>
{
    public AttestGuardianRequestValidator()
    {
        RuleFor(x => x.GuardianPartyId).NotEmpty();
        RuleFor(x => x.EvidenceRef).MaximumLength(256);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

internal sealed class RevokeAttestationRequestValidator : Validator<RevokeAttestationRequest>
{
    public RevokeAttestationRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(512);
}
