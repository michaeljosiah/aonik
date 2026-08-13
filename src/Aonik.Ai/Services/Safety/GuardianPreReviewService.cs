using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Spec 096 §8 — guardian pre-review, and the disclosure that makes it not-surveillance.
///
/// <para>
/// Pre-review runs <strong>after</strong> the automated layers, holding content they already allowed.
/// That ordering is the point: approval here can only release something already judged safe, so a
/// guardian can never click past the gate. It is also why turning pre-review off is permitted — a
/// parent is not a moderation queue and will approve in bulk within a week, so the layers beneath must
/// stand alone whether or not anyone is watching.
/// </para>
/// </summary>
public interface IGuardianPreReviewService
{
    /// <summary>
    /// Whether this child's allowed content is held before delivery. Called by the gate.
    /// </summary>
    Task<bool> RequiresPreReviewAsync(
        Guid subjectPartyId, string safetyBand, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingReviewItem>> ListPendingAsync(
        Guid guardianPartyId, Guid childPartyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Release held content to the child. Yields the permit that authorises delivery — the same
    /// unforgeable token <see cref="IContentSafetyGate"/> issues, carrying the original decision id.
    /// </summary>
    Task<PreReviewDecision> ApproveAsync(
        Guid guardianPartyId, Guid pendingReviewId, CancellationToken cancellationToken = default);

    Task<PreReviewDecision> DeclineAsync(
        Guid guardianPartyId, Guid pendingReviewId, CancellationToken cancellationToken = default);

    /// <summary>Turn pre-review on or off for one child. Recorded against the guardian who chose.</summary>
    Task SetPreReviewAsync(
        Guid guardianPartyId, Guid childPartyId, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// What the child is told about adult oversight. Not a setting — see <see cref="SupervisionDisclosure"/>.
    /// </summary>
    Task<SupervisionDisclosure> DescribeSupervisionAsync(
        Guid childPartyId, string safetyBand, CancellationToken cancellationToken = default);
}

public sealed record PendingReviewItem(
    Guid PendingReviewId,
    Guid SubjectPartyId,
    string Modality,
    string Reference,
    DateTime HeldAt,
    DateTime ExpiresAt);

/// <param name="Permit">
/// Non-null only on approval. Absence is the enforcement, exactly as with a blocked verdict: a caller
/// that ignores a decline has nothing to deliver with.
/// </param>
public sealed record PreReviewDecision(PreReviewOutcome Outcome, ContentDeliveryPermit? Permit = null);

public enum PreReviewOutcome
{
    Approved = 0,
    Declined = 1,

    /// <summary>Already decided, expired, or no such hold.</summary>
    NotAvailable = 2,
}

/// <summary>
/// What a child is told about who can see their work (Spec 096 §8).
///
/// <para>
/// <see cref="GuardianCanSee"/> has no off switch and is not read from configuration. The Children's
/// Code is explicit that covert parental monitoring is unacceptable, and the way to honour that is to
/// make the covert mode <strong>unbuildable</strong> rather than merely discouraged: there is no state
/// of this system in which an adult can see a child's work and the child has not been told.
/// </para>
/// </summary>
/// <param name="ChildFacingMessage">
/// Age-appropriate, and never mentions a category or a filter. A seven-year-old told they triggered a
/// "violence filter" learns they did something wrong; they did not.
/// </param>
public sealed record SupervisionDisclosure(
    bool GuardianCanSee,
    bool GuardianReviewsBeforeDelivery,
    string ChildFacingMessage);

internal sealed class GuardianPreReviewService : IGuardianPreReviewService
{
    private readonly AiDbContext _dbContext;
    private readonly IGuardianshipReader _guardianship;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ILogger<GuardianPreReviewService> _logger;

    public GuardianPreReviewService(
        AiDbContext dbContext,
        IGuardianshipReader guardianship,
        ITenantProvider tenantProvider,
        IClock clock,
        ILogger<GuardianPreReviewService> logger)
    {
        _dbContext = dbContext;
        _guardianship = guardianship;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> RequiresPreReviewAsync(
        Guid subjectPartyId, string safetyBand, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var preference = await _dbContext.ChildSafetyPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.SubjectPartyId == subjectPartyId, cancellationToken);

        // An explicit guardian choice wins. Absence is NOT "off" — it falls through to the band
        // default, so a child whose preference row was never written still gets pre-review where the
        // band says so.
        return preference?.PreReviewEnabled ?? PreReviewDefaults.DefaultFor(safetyBand);
    }

    public async Task<IReadOnlyList<PendingReviewItem>> ListPendingAsync(
        Guid guardianPartyId, Guid childPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await RequireAuthorityAsync(tenantId, guardianPartyId, childPartyId, cancellationToken);

        var now = _clock.UtcNow;

        var held = await _dbContext.PendingContentReviews
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.SubjectPartyId == childPartyId
                && r.State == PreReviewStates.Pending
                && r.ExpiresAt > now)
            .OrderBy(r => r.HeldAt)
            .ToListAsync(cancellationToken);

        return [.. held.Select(r => new PendingReviewItem(
            r.Id, r.SubjectPartyId, r.Modality, r.Reference, r.HeldAt, r.ExpiresAt))];
    }

    public Task<PreReviewDecision> ApproveAsync(
        Guid guardianPartyId, Guid pendingReviewId, CancellationToken cancellationToken = default)
        => DecideAsync(guardianPartyId, pendingReviewId, approve: true, cancellationToken);

    public Task<PreReviewDecision> DeclineAsync(
        Guid guardianPartyId, Guid pendingReviewId, CancellationToken cancellationToken = default)
        => DecideAsync(guardianPartyId, pendingReviewId, approve: false, cancellationToken);

    private async Task<PreReviewDecision> DecideAsync(
        Guid guardianPartyId, Guid pendingReviewId, bool approve, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var review = await _dbContext.PendingContentReviews
            .FirstOrDefaultAsync(
                r => r.TenantId == tenantId && r.Id == pendingReviewId, cancellationToken);

        if (review is null)
        {
            return new PreReviewDecision(PreReviewOutcome.NotAvailable);
        }

        await RequireAuthorityAsync(tenantId, guardianPartyId, review.SubjectPartyId, cancellationToken);

        var now = _clock.UtcNow;

        if (review.State != PreReviewStates.Pending)
        {
            return new PreReviewDecision(PreReviewOutcome.NotAvailable);
        }

        if (review.ExpiresAt <= now)
        {
            // Expiry is recorded as expiry, never resolved as approval. An unattended queue must not
            // become an approval mechanism, which is what any "auto-approve after N days" would make it.
            review.State = PreReviewStates.Expired;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new PreReviewDecision(PreReviewOutcome.NotAvailable);
        }

        review.State = approve ? PreReviewStates.Approved : PreReviewStates.Declined;
        review.DecidedByPartyId = guardianPartyId;
        review.DecidedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!approve)
        {
            return new PreReviewDecision(PreReviewOutcome.Declined);
        }

        // The permit carries the ORIGINAL decision id, not a new one. Nothing was re-judged here — the
        // content passed L1–L4 before it was ever held, and a guardian releasing it is releasing that
        // verdict, which is what keeps delivery traceable to the classifiers that actually ran.
        return new PreReviewDecision(
            PreReviewOutcome.Approved,
            new ContentDeliveryPermit(review.SafetyDecisionId, review.SubjectPartyId, review.SafetyBand));
    }

    public async Task SetPreReviewAsync(
        Guid guardianPartyId, Guid childPartyId, bool enabled, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        await RequireAuthorityAsync(tenantId, guardianPartyId, childPartyId, cancellationToken);

        var now = _clock.UtcNow;

        var preference = await _dbContext.ChildSafetyPreferences
            .FirstOrDefaultAsync(
                p => p.TenantId == tenantId && p.SubjectPartyId == childPartyId, cancellationToken);

        if (preference is null)
        {
            preference = new ChildSafetyPreference
            {
                TenantId = tenantId,
                SubjectPartyId = childPartyId,
            };
            _dbContext.ChildSafetyPreferences.Add(preference);
        }

        preference.PreReviewEnabled = enabled;
        preference.SetByPartyId = guardianPartyId;
        preference.SetAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!enabled)
        {
            // Worth a line in the log rather than a silent write: this is a guardian choosing to stop
            // looking, and the automated layers are now the only thing between the model and a child —
            // which they were designed to be, but the moment it becomes true should be recorded.
            _logger.LogInformation(
                "Guardian {GuardianId} disabled pre-review for {ChildId}.", guardianPartyId, childPartyId);
        }
    }

    public async Task<SupervisionDisclosure> DescribeSupervisionAsync(
        Guid childPartyId, string safetyBand, CancellationToken cancellationToken = default)
    {
        var preReview = await RequiresPreReviewAsync(childPartyId, safetyBand, cancellationToken);

        return new SupervisionDisclosure(
            // Constant, not configuration. A guardian can always see what was generated for their
            // child, and the child is always told — there is no combination of settings that produces
            // covert monitoring, because neither half is settable.
            GuardianCanSee: true,
            GuardianReviewsBeforeDelivery: preReview,
            ChildFacingMessage: ChildFacingWording.For(safetyBand, preReview));
    }

    private async Task RequireAuthorityAsync(
        Guid tenantId, Guid guardianPartyId, Guid childPartyId, CancellationToken cancellationToken)
    {
        if (!await _guardianship.HasAuthorityAsync(tenantId, guardianPartyId, childPartyId, cancellationToken))
        {
            throw new GuardianAuthorityRequiredException(guardianPartyId, childPartyId);
        }
    }
}

internal static class PreReviewDefaults
{
    /// <summary>
    /// On for the youngest band, and on for a band we cannot establish — the same wrong-way-default
    /// rule as everywhere else here. Off by default for older bands, where holding every story would
    /// make the product unusable and train the guardian to approve without looking.
    /// </summary>
    public static bool DefaultFor(string safetyBand) => safetyBand switch
    {
        SafetyBandNames.Under6 => true,
        SafetyBandNames.Age6To9 => false,
        SafetyBandNames.Age10To12 => false,
        SafetyBandNames.Age13ToMajority => false,
        SafetyBandNames.Adult => false,
        _ => true,
    };
}

/// <summary>
/// What the child reads. Plain, unalarming, and honest about the adult in the loop (Spec 096 §8, §10.1).
/// </summary>
internal static class ChildFacingWording
{
    public static string For(string safetyBand, bool preReview) => safetyBand switch
    {
        SafetyBandNames.Under6 or SafetyBandNames.Age6To9 => preReview
            ? "A grown-up who looks after you sees your stories before you do."
            : "A grown-up who looks after you can see your stories.",

        _ => preReview
            ? "Your parent or guardian reviews what you make here before you see it."
            : "Your parent or guardian can see what you make here.",
    };
}
