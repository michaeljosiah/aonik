using Aonik.Platform.Contracts.Api.Consent;
using Aonik.Platform.Services.Consent;
using Aonik.SharedKernel.Abstractions;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Consent;

/// <summary>
/// The operator half of the signed-form verification route (Spec 095 §8, aonik#328): a named member of
/// staff records that they have read a returned form and matched it to a named adult. From then on,
/// and until the attestation expires or is revoked, that adult can enrol children and grant purposes.
///
/// <para>
/// This is the manual procedure the route needs, made visible: it is an admin action, audited under the
/// user who performed it, and never reachable by the guardian it concerns.
/// </para>
/// </summary>
internal sealed class AttestGuardianEndpoint : Endpoint<AttestGuardianRequest, AttestGuardianResponse>
{
    private readonly IGuardianAttestationService _attestations;
    private readonly ICurrentUserProvider _currentUser;

    public AttestGuardianEndpoint(IGuardianAttestationService attestations, ICurrentUserProvider currentUser)
    {
        _attestations = attestations;
        _currentUser = currentUser;
    }

    public override void Configure()
    {
        Post("/admin/consent/attestations");
        Policies("AdminWritePolicy");
        Summary(s =>
        {
            s.Summary = "Record a signed-form guardian attestation";
            s.Description = "An operator records that a signed consent form was read and matched to the named guardian party. "
                + "The attestation makes the signed-form verification route available to that guardian until it expires or is revoked.";
            s.Response(201, "Attestation recorded");
            s.Response(422, "No guardian party named");
        });
        Options(x => x.WithTags("Consent"));
    }

    public override async Task HandleAsync(AttestGuardianRequest req, CancellationToken ct)
    {
        if (!_currentUser.TryGetCurrentUserId(out var attestedBy))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var attestationId = await _attestations.AttestAsync(req.GuardianPartyId, attestedBy, req.EvidenceRef, req.Notes, ct);

        await Send.CreatedAtAsync<ListWardsEndpoint>(
            null, new AttestGuardianResponse(attestationId, req.GuardianPartyId), cancellation: ct);
    }
}

internal sealed class RevokeAttestationEndpoint : Endpoint<RevokeAttestationRequest, EmptyResponse>
{
    private readonly IGuardianAttestationService _attestations;

    public RevokeAttestationEndpoint(IGuardianAttestationService attestations) => _attestations = attestations;

    public override void Configure()
    {
        Delete("/admin/consent/attestations/{attestationId:guid}");
        Policies("AdminWritePolicy");
        Summary(s =>
        {
            s.Summary = "Revoke a guardian attestation";
            s.Description = "Withdraws an attestation — a form found to be forged, an arrangement that ended. Grants already made "
                + "stand; the attestation stops supporting new ones.";
            s.Response(204, "Attestation revoked");
            s.Response(422, "No reason given");
        });
        Options(x => x.WithTags("Consent"));
    }

    public override async Task HandleAsync(RevokeAttestationRequest req, CancellationToken ct)
    {
        await _attestations.RevokeAsync(Route<Guid>("attestationId"), req.Reason.Trim(), ct);
        await Send.NoContentAsync(ct);
    }
}
