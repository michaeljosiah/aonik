using Microsoft.Extensions.Options;

using Aonik.Publishing.Entities;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Modules;

namespace Aonik.Publishing.Services;

internal sealed class EditorialReviewAuthorization(ITenantProvider tenantProvider, ICurrentUserContext user,
    ICurrentPartyResolver parties, IUserPartyMembershipReader memberships, IPermissionService permissions,
    IModuleGate moduleGate, IOptions<PublishingOptions> configuration)
{
    public async Task<ReviewerActor> RequireAsync(CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.UserId is not { } userId || userId == Guid.Empty)
            throw new PublishingException(401, "unauthenticated", "A signed-in editor is required.");
        var tenant = tenantProvider.GetCurrentTenantId();
        if (tenant == Guid.Empty || user.TenantId != tenant) throw Forbidden();
        await moduleGate.EnsureEnabledAsync(tenant, ModuleIds.Publishing, ct);
        if (!await permissions.HasPermissionAsync(userId, "Publishing.Review", ct)) throw Forbidden();
        var party = await parties.GetCurrentPartyIdAsync(ct);
        var linked = await memberships.GetPartyIdsAsync(tenant, userId, ct);
        if (party is null || party == Guid.Empty || !linked.Contains(party.Value)) throw Forbidden();
        return new(tenant, userId, party.Value, linked);
    }

    public PublishingChannelOptions RequireChannel(ReviewerActor actor, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !configuration.Value.Channels.TryGetValue(key, out var channel) || channel.TenantId != actor.TenantId
            || !channel.ReviewEnabled || !channel.ReviewerUserIds.Contains(actor.UserId)
            || string.IsNullOrWhiteSpace(channel.ReviewPolicyVersion) || channel.ReviewPolicyVersion.Length > 120
            || channel.ReviewPolicyVersion.Any(char.IsControl))
            throw new PublishingException(404, "review-unavailable", "No authorised publisher review is available.");
        return channel;
    }

    public static void RequireIndependent(ReviewerActor actor, PublicationSubmission submission, SubmissionRevision revision)
    {
        if (submission.OriginatingUserId == actor.UserId || revision.OriginatingUserId == actor.UserId
            || revision.CreatedBy == actor.UserId || actor.PartyIds.Contains(submission.CreatorPartyId))
            throw new PublishingException(403, "self-review-forbidden", "An independent editor must review this submission.");
    }
    private static PublishingException Forbidden() => new(403, "review-forbidden", "Publisher review permission and an active reviewer party are required.");
}

internal sealed record ReviewerActor(Guid TenantId, Guid UserId, Guid PartyId, IReadOnlyList<Guid> PartyIds);
