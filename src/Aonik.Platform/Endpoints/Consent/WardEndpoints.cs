using Aonik.Platform.Contracts.Api.Consent;
using Aonik.Platform.Services.Consent;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;

using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Consent;

/// <summary>
/// The guardian-facing consent endpoints (Spec 095 §12, aonik#326): a guardian sees their wards and
/// the consent that stands, enrols a child, grants a further purpose, or withdraws one.
///
/// <para>
/// Everything acts as the caller's party, resolved server-side. Enrolment and grants verify the
/// guardian through the platform's own route resolution and record every attempt; a caller cannot
/// name a verification method. A child who is not the caller's ward answers 404, never 403, so
/// another family's children are not enumerable.
/// </para>
/// </summary>
internal abstract class ConsentEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected const string Tag = "Consent";
    protected const string GuardianPolicy = "UserPolicy";

    protected async Task<Guid?> CallerPartyAsync(CancellationToken ct)
    {
        var partyId = await Resolve<ICurrentPartyResolver>().GetCurrentPartyIdAsync(ct);

        if (partyId is null)
        {
            await ProblemAsync(new ConsentProblem(
                StatusCodes.Status403Forbidden, "no-party", "The signed-in user is not linked to a party, so cannot be a guardian."));
        }

        return partyId;
    }

    protected Task ProblemAsync(ConsentProblem problem)
        => Send.ResultAsync(Results.Json(problem, statusCode: problem.Status));

    protected async Task GuardedAsync(Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (Exception ex) when (ConsentProblems.From(ex) is { } problem)
        {
            await ProblemAsync(problem);
        }
    }

    protected static WardResponse ToResponse(WardInfo ward) => new(
        ward.ChildPartyId,
        ward.DisplayName,
        ward.ConsentBand,
        ward.SafetyBand,
        ward.ConsentAgeOn,
        ward.MajorityOn,
        [.. ward.Grants.Select(g => new ConsentGrantResponse(g.Purpose, g.TermsVersion, g.VerificationMethod, g.GrantedByPartyId, g.GrantedAt, g.ExpiresAt))]);
}

internal static class ConsentProblems
{
    public const string WardNotFound = "ward-not-found";

    public static ConsentProblem NoSuchWard() => new(StatusCodes.Status404NotFound, WardNotFound, "No such ward is available to you.");

    public static ConsentProblem? From(Exception exception) => exception switch
    {
        // Same answer as an absent child: authority is the only thing that makes a child visible.
        GuardianAuthorityRequiredException => NoSuchWard(),
        GuardianVerificationFailedException failed => new(
            StatusCodes.Status403Forbidden, "guardian-not-verified",
            $"The guardian could not be verified: {failed.Message}. Complete a verification route first."),
        ConsentRequiredException required => new(StatusCodes.Status403Forbidden, "consent-required", required.Message),
        ArgumentException invalid => new(StatusCodes.Status422UnprocessableEntity, "invalid-request", invalid.Message),
        InvalidOperationException invalid => new(StatusCodes.Status422UnprocessableEntity, "invalid-request", invalid.Message),
        _ => null,
    };
}

internal sealed class ListWardsEndpoint : ConsentEndpoint<EmptyRequest, WardListResponse>
{
    private readonly IWardReader _wards;

    public ListWardsEndpoint(IWardReader wards) => _wards = wards;

    public override void Configure()
    {
        Get("/consent/wards");
        Policies(GuardianPolicy);
        Summary(s =>
        {
            s.Summary = "List my wards";
            s.Description = "The children the caller's party holds active guardian authority over, with the consent that stands for each.";
            s.Response(200, "Wards returned");
            s.Response(403, "The caller has no party");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        if (await CallerPartyAsync(ct) is not { } guardian)
        {
            return;
        }

        var wards = await _wards.ListAsync(guardian, ct);
        await Send.OkAsync(new WardListResponse([.. wards.Select(ToResponse)]), ct);
    }
}

internal sealed class GetWardEndpoint : ConsentEndpoint<EmptyRequest, WardResponse>
{
    private readonly IWardReader _wards;

    public GetWardEndpoint(IWardReader wards) => _wards = wards;

    public override void Configure()
    {
        Get("/consent/wards/{childPartyId:guid}");
        Policies(GuardianPolicy);
        Summary(s =>
        {
            s.Summary = "Get a ward";
            s.Description = "One child the caller holds guardian authority over, with the consent that stands. Any other party answers 404.";
            s.Response(200, "Ward returned");
            s.Response(404, "No such ward is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var childPartyId = Route<Guid>("childPartyId");

        if (await CallerPartyAsync(ct) is not { } guardian)
        {
            return;
        }

        var ward = await _wards.GetAsync(guardian, childPartyId, ct);

        if (ward is null)
        {
            await ProblemAsync(ConsentProblems.NoSuchWard());
            return;
        }

        await Send.OkAsync(ToResponse(ward), ct);
    }
}

/// <summary>
/// Enrol a child: the child party, the guardian edge and the first grants in one transaction, gated on
/// the guardian's verification (Spec 095 §12.2). There is no other way to create a child.
/// </summary>
internal sealed class EnrolWardEndpoint : ConsentEndpoint<EnrolWardRequest, EnrolWardResponse>
{
    private readonly IConsentService _consent;

    public EnrolWardEndpoint(IConsentService consent) => _consent = consent;

    public override void Configure()
    {
        Post("/consent/wards");
        Policies(GuardianPolicy);
        Summary(s =>
        {
            s.Summary = "Enrol a child";
            s.Description = "Creates the child, the caller's guardian edge and the service-core grant (plus any purposes named) in one "
                + "transaction, after verifying the caller through an accepted route for the jurisdiction. Nothing is created when "
                + "verification is unavailable or refuses.";
            s.Response(201, "Child enrolled");
            s.Response(403, "The caller has no party, or could not be verified as a guardian");
            s.Response(422, "Missing date of birth, unknown purpose or jurisdiction");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EnrolWardRequest req, CancellationToken ct)
    {
        if (await CallerPartyAsync(ct) is not { } guardian)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var enrolled = await _consent.EnrolChildAsync(new EnrolChildRequest(
                guardian,
                req.DisplayName.Trim(),
                req.DateOfBirth,
                req.Jurisdiction,
                req.TermsVersion.Trim(),
                req.Purposes ?? []), ct);

            await Send.CreatedAtAsync<GetWardEndpoint>(
                new { childPartyId = enrolled.ChildPartyId },
                new EnrolWardResponse(
                    enrolled.ChildPartyId, enrolled.EnrolmentAttemptId, enrolled.VerificationMethod,
                    enrolled.ConsentAgeOn, enrolled.MajorityOn, enrolled.SafetyBand),
                cancellation: ct);
        });
    }
}

internal sealed class GrantPurposeEndpoint : ConsentEndpoint<GrantPurposeRequest, GrantPurposeResponse>
{
    private readonly IConsentService _consent;

    public GrantPurposeEndpoint(IConsentService consent) => _consent = consent;

    public override void Configure()
    {
        Post("/consent/wards/{childPartyId:guid}/purposes");
        Policies(GuardianPolicy);
        Summary(s =>
        {
            s.Summary = "Grant a purpose for a ward";
            s.Description = "Grants one consent purpose for a child the caller holds guardian authority over, after verifying the caller "
                + "again through an accepted route. Re-granting the same terms version is idempotent; a new version supersedes the old grant.";
            s.Response(200, "Purpose granted");
            s.Response(403, "The caller could not be verified as a guardian");
            s.Response(404, "No such ward is available to the caller");
            s.Response(422, "Unknown purpose");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(GrantPurposeRequest req, CancellationToken ct)
    {
        var childPartyId = Route<Guid>("childPartyId");

        if (await CallerPartyAsync(ct) is not { } guardian)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            var method = await _consent.GrantByGuardianAsync(new GrantByGuardianRequest(
                childPartyId, guardian, req.Purpose.Trim().ToLowerInvariant(), req.TermsVersion.Trim(), req.Jurisdiction), ct);

            await Send.OkAsync(new GrantPurposeResponse(req.Purpose.Trim().ToLowerInvariant(), method), ct);
        });
    }
}

/// <summary>
/// A second parent (Spec 095 §7): an existing guardian authorises the addition, and the new guardian is
/// verified to the same standard as the first. Each guardian then acts independently — enrolment,
/// grants, withdrawals, and through the Workspaces resolver the child's worlds (Spec 095 §12).
/// </summary>
internal sealed class AddWardGuardianEndpoint : ConsentEndpoint<AddWardGuardianRequest, AddWardGuardianResponse>
{
    private readonly IConsentService _consent;

    public AddWardGuardianEndpoint(IConsentService consent) => _consent = consent;

    public override void Configure()
    {
        Post("/consent/wards/{childPartyId:guid}/guardians");
        Policies(GuardianPolicy);
        Summary(s =>
        {
            s.Summary = "Add a guardian for a ward";
            s.Description = "Gives another adult active guardian authority over a child the caller already holds it for. The new "
                + "guardian is verified through an accepted route for the jurisdiction, to the same standard as the first; "
                + "the caller cannot add themselves. Idempotent for a party who is already a guardian.";
            s.Response(200, "Guardian added, or already a guardian");
            s.Response(403, "The new guardian could not be verified");
            s.Response(404, "No such ward is available to the caller");
            s.Response(422, "The caller named themselves");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(AddWardGuardianRequest req, CancellationToken ct)
    {
        var childPartyId = Route<Guid>("childPartyId");

        if (await CallerPartyAsync(ct) is not { } guardian)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            await _consent.AddGuardianAsync(new AddGuardianRequest(
                childPartyId, req.GuardianPartyId, guardian, req.Jurisdiction), ct);

            await Send.OkAsync(new AddWardGuardianResponse(childPartyId, req.GuardianPartyId), ct);
        });
    }
}

internal sealed class WithdrawPurposeEndpoint : ConsentEndpoint<EmptyRequest, WithdrawPurposeResponse>
{
    private readonly IConsentService _consent;

    public WithdrawPurposeEndpoint(IConsentService consent) => _consent = consent;

    public override void Configure()
    {
        Delete("/consent/wards/{childPartyId:guid}/purposes/{purpose}");
        Policies(GuardianPolicy);
        Summary(s =>
        {
            s.Summary = "Withdraw a purpose for a ward";
            s.Description = "Withdraws one consent purpose. Any single active guardian may withdraw, and it takes effect on the next "
                + "operation regardless of other guardians (Spec 095 §7.1). Withdrawing service-core withdraws the child's participation.";
            s.Response(200, "Purpose withdrawn, or nothing stood to withdraw");
            s.Response(404, "No such ward is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var childPartyId = Route<Guid>("childPartyId");
        var purpose = Route<string>("purpose")?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!ConsentPurposes.All.Contains(purpose))
        {
            ThrowError($"Unknown consent purpose '{purpose}'.", StatusCodes.Status422UnprocessableEntity);
        }

        if (await CallerPartyAsync(ct) is not { } guardian)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            await _consent.WithdrawAsync(new WithdrawConsentRequest(childPartyId, guardian, purpose), ct);
            await Send.OkAsync(new WithdrawPurposeResponse(purpose, true), ct);
        });
    }
}
