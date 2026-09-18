using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services.Safety;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;

using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Endpoints.Safety;

/// <summary>
/// The content-safety boundary over HTTP (Spec 096; aonik#323 for ArkeKidz#10).
///
/// <para>
/// Everything acts as the caller's party. The subject is a child the caller holds guardian authority
/// over, or the caller themselves; anyone else's child answers 404, never 403, so children are not
/// enumerable. Classification is egress (Spec 095 §12.3), so the subject's <c>safety-classification</c>
/// consent must stand, and the route the classifier takes must be one their terms name — the gate
/// refuses when it is not, and refuses when it cannot check at all. A verdict never carries anything a
/// caller could deliver with except its own record: the decision id, and the hash of exactly what was
/// judged, which a delivery check compares against the bytes in hand.
/// </para>
/// </summary>
internal abstract class SafetyEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
{
    protected const string Tag = "Content safety";
    protected const string UserPolicy = "UserPolicy";

    protected async Task<Guid?> CallerPartyAsync(CancellationToken ct)
    {
        var partyId = await Resolve<ICurrentPartyResolver>().GetCurrentPartyIdAsync(ct);

        if (partyId is null)
        {
            await ProblemAsync(new SafetyProblem(StatusCodes.Status403Forbidden, "no-party", "The signed-in user is not linked to a party."));
        }

        return partyId;
    }

    protected Task ProblemAsync(SafetyProblem problem)
        => Send.ResultAsync(Results.Json(problem, statusCode: problem.Status));

    protected async Task GuardedAsync(Func<Task> body)
    {
        try
        {
            await body();
        }
        catch (Exception ex) when (SafetyProblems.From(ex) is { } problem)
        {
            await ProblemAsync(problem);
        }
    }

    protected static string OutcomeName(SafetyDecisionOutcome outcome) => outcome switch
    {
        SafetyDecisionOutcome.Allowed => "allowed",
        SafetyDecisionOutcome.Blocked => "blocked",
        SafetyDecisionOutcome.CheckUnavailable => "check-unavailable",
        SafetyDecisionOutcome.HeldForReview => "held-for-review",
        SafetyDecisionOutcome.ModalityDisabled => "modality-disabled",
        _ => outcome.ToString().ToLowerInvariant(),
    };

    protected static string OutcomeName(string stored)
        => Enum.TryParse<SafetyDecisionOutcome>(stored, out var outcome) ? OutcomeName(outcome) : stored.ToLowerInvariant();
}

internal static class SafetyProblems
{
    public static SafetyProblem NoSuchSubject() => new(StatusCodes.Status404NotFound, "ward-not-found", "No such ward is available to you.");

    public static SafetyProblem NoSuchDecision() => new(StatusCodes.Status404NotFound, "decision-not-found", "No such decision is available to you.");

    public static SafetyProblem? From(Exception exception) => exception switch
    {
        GuardianAuthorityRequiredException => NoSuchSubject(),
        ConsentRequiredException required => new(StatusCodes.Status403Forbidden, "consent-required", required.Message),
        ArgumentException invalid => new(StatusCodes.Status422UnprocessableEntity, "invalid-request", invalid.Message),
        _ => null,
    };
}

/// <summary>Screen text for a child: what they typed (L2) or what a model produced for them (L4).</summary>
internal sealed class ScreenContentEndpoint : SafetyEndpoint<ScreenContentRequest, SafetyVerdictResponse>
{
    private readonly IContentSafetyGate _gate;
    private readonly IConsentGate _consent;
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public ScreenContentEndpoint(IContentSafetyGate gate, IConsentGate consent, AiDbContext dbContext, ITenantProvider tenantProvider)
    {
        _gate = gate;
        _consent = consent;
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Post("/safety/screen");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Screen content for a child";
            s.Description = "Classifies text against the child's age band and the current policy, records the decision, and answers "
                + "the verdict with the hash of exactly what was judged. Input (layer 'input') is what the child typed, before a "
                + "model sees it; output (layer 'output') is what a model produced, before the child does. An output the child's "
                + "band holds for guardian review answers 'held-for-review' with the review to approve. A check that cannot run "
                + "answers 'check-unavailable' and is a refusal.";
            s.Response(200, "Decision recorded");
            s.Response(403, "The caller has no party, or the child's safety-classification consent does not stand");
            s.Response(404, "The subject is not a child the caller holds guardian authority over");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(ScreenContentRequest req, CancellationToken ct)
    {
        if (await CallerPartyAsync(ct) is not { } caller)
        {
            return;
        }

        await GuardedAsync(async () =>
        {
            await _consent.EnsureCanActForAsync(caller, req.SubjectPartyId, ct);
            await _consent.EnsureAsync(req.SubjectPartyId, ConsentPurposes.SafetyClassification, ct);

            var modality = req.Modality.Trim().ToLowerInvariant();
            var request = new SafetyRequest(req.SubjectPartyId, modality, req.GenerationRunId, req.UsageReservationId);

            var verdict = string.Equals(req.Layer.Trim(), "input", StringComparison.OrdinalIgnoreCase)
                ? await _gate.ScreenInputAsync(request, req.Content, ct)
                : await _gate.ScreenOutputAsync(request, new GeneratedContent(modality, req.Content), ct);

            var tenantId = _tenantProvider.GetCurrentTenantId();
            var decision = await _dbContext.SafetyDecisions.AsNoTracking()
                .FirstAsync(d => d.TenantId == tenantId && d.Id == verdict.DecisionId, ct);

            Guid? pendingReviewId = verdict.Outcome == SafetyDecisionOutcome.HeldForReview
                ? await _dbContext.PendingContentReviews.AsNoTracking()
                    .Where(r => r.TenantId == tenantId && r.SafetyDecisionId == verdict.DecisionId)
                    .Select(r => (Guid?)r.Id)
                    .FirstOrDefaultAsync(ct)
                : null;

            await Send.OkAsync(new SafetyVerdictResponse(
                verdict.DecisionId,
                verdict.Allowed,
                OutcomeName(verdict.Outcome),
                verdict.Categories,
                decision.ContentHash ?? ContentHashes.Of(req.Content),
                decision.SafetyBand,
                decision.SafetyPolicyVersion,
                pendingReviewId), ct);
        });
    }
}

/// <summary>A decision as recorded: what a delivery check reads back before trusting a product's copy of it.</summary>
internal sealed class GetSafetyDecisionEndpoint : SafetyEndpoint<EmptyRequest, SafetyDecisionResponse>
{
    private readonly IConsentGate _consent;
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public GetSafetyDecisionEndpoint(IConsentGate consent, AiDbContext dbContext, ITenantProvider tenantProvider)
    {
        _consent = consent;
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Get("/safety/decisions/{decisionId:guid}");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Get a safety decision";
            s.Description = "The decision as recorded — outcome, categories, policy version, the hash of what was judged, and the "
                + "guardian review it is held for, if any. A decision about a child the caller may not act for answers 404.";
            s.Response(200, "Decision returned");
            s.Response(404, "No such decision is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        if (await CallerPartyAsync(ct) is not { } caller)
        {
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var decision = await _dbContext.SafetyDecisions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == Route<Guid>("decisionId"), ct);

        if (decision is null || !await MayActForAsync(caller, decision.SubjectPartyId, ct))
        {
            await ProblemAsync(SafetyProblems.NoSuchDecision());
            return;
        }

        var review = await _dbContext.PendingContentReviews.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.SafetyDecisionId == decision.Id)
            .Select(r => new PendingReviewModel(r.Id, r.State, r.HeldAt, r.ExpiresAt, r.DecidedByPartyId, r.DecidedAt))
            .FirstOrDefaultAsync(ct);

        await Send.OkAsync(new SafetyDecisionResponse(
            decision.Id,
            decision.SubjectPartyId,
            decision.SafetyBand,
            decision.Modality,
            decision.Layer,
            OutcomeName(decision.Outcome),
            decision.Categories?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [],
            decision.SafetyPolicyVersion,
            decision.ContentHash,
            decision.DecidedAt,
            review), ct);
    }

    private async Task<bool> MayActForAsync(Guid caller, Guid subject, CancellationToken ct)
    {
        try
        {
            await _consent.EnsureCanActForAsync(caller, subject, ct);
            return true;
        }
        catch (GuardianAuthorityRequiredException)
        {
            return false;
        }
    }
}

/// <summary>The guardian's own decision on content held for review (L5): approve it for delivery, or decline it.</summary>
internal sealed class ReviewSafetyDecisionEndpoint : SafetyEndpoint<ReviewDecisionRequest, ReviewDecisionResponse>
{
    private readonly IGuardianPreReviewService _reviews;
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public ReviewSafetyDecisionEndpoint(IGuardianPreReviewService reviews, AiDbContext dbContext, ITenantProvider tenantProvider)
    {
        _reviews = reviews;
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Post("/safety/decisions/{decisionId:guid}/review");
        Policies(UserPolicy);
        Summary(s =>
        {
            s.Summary = "Review held content";
            s.Description = "A guardian approves or declines content held for their review. Approval is what lets the product deliver "
                + "it; declining leaves it undeliverable. Already decided, expired, or not this caller's ward: 'not-available'.";
            s.Response(200, "Review recorded, or not available");
            s.Response(404, "No such decision is available to the caller");
        });
        Options(x => x.WithTags(Tag));
    }

    public override async Task HandleAsync(ReviewDecisionRequest req, CancellationToken ct)
    {
        if (await CallerPartyAsync(ct) is not { } caller)
        {
            return;
        }

        var decisionId = Route<Guid>("decisionId");
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var review = await _dbContext.PendingContentReviews.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.SafetyDecisionId == decisionId, ct);

        if (review is null)
        {
            await ProblemAsync(SafetyProblems.NoSuchDecision());
            return;
        }

        await GuardedAsync(async () =>
        {
            var approve = string.Equals(req.Decision.Trim(), "approve", StringComparison.OrdinalIgnoreCase);
            var decided = approve
                ? await _reviews.ApproveAsync(caller, review.Id, ct)
                : await _reviews.DeclineAsync(caller, review.Id, ct);

            var contentHash = await _dbContext.SafetyDecisions.AsNoTracking()
                .Where(d => d.TenantId == tenantId && d.Id == decisionId)
                .Select(d => d.ContentHash)
                .FirstOrDefaultAsync(ct);

            await Send.OkAsync(new ReviewDecisionResponse(decisionId, decided.Outcome switch
            {
                PreReviewOutcome.Approved => "approved",
                PreReviewOutcome.Declined => "declined",
                _ => "not-available",
            }, decided.Outcome == PreReviewOutcome.Approved ? contentHash : null), ct);
        });
    }
}
