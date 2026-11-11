using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Aonik.Publishing.Contracts;
using Aonik.Publishing.Entities;
using Aonik.Publishing.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Publishing.Services;

internal sealed class CreatorSubmissionService(
    IPublishingDataContext dbContext,
    ITenantProvider tenantProvider,
    ICurrentUserContext user,
    ICurrentPartyResolver parties,
    IPermissionService permissions,
    IModuleGate moduleGate,
    IOptions<PublishingOptions> options) : ICreatorSubmissionService
{
    public async Task<CreateSubmissionResult> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(write: true, cancellationToken);
        request = SubmissionDeclaration.Normalize(request);
        RequireChannel(actor.TenantId, request);
        var digest = SubmissionDeclaration.Digest(request);
        var existing = await FindRequestAsync(actor.TenantId, actor.PartyId, request.ClientRequestId, cancellationToken);
        if (existing is not null)
            return await ReplayAsync(existing, digest, cancellationToken);

        var revisionId = Guid.NewGuid();
        var submission = new PublicationSubmission
        {
            Id = Guid.NewGuid(), TenantId = actor.TenantId, CreatorPartyId = actor.PartyId,
            OriginatingUserId = actor.UserId, ClientRequestId = request.ClientRequestId,
            ChannelKey = request.ChannelKey, CurrentRevisionId = revisionId,
        };
        var revision = new SubmissionRevision
        {
            Id = revisionId, TenantId = actor.TenantId, SubmissionId = submission.Id, Number = 1,
            RequestDigest = digest, Title = request.Title, CreatorDisplayName = request.CreatorDisplayName,
            Summary = request.Summary, Language = request.Language, WorldId = request.WorldId,
            Edition = request.Edition, PackageFileName = request.PackageFileName,
            DeclaredPackageBytes = request.DeclaredPackageBytes, DeclaredPackageSha256 = request.DeclaredPackageSha256,
            RightsDeclarationVersion = request.RightsDeclarationVersion,
            DistributionPermissionGranted = request.DistributionPermissionGranted,
            EditorialMaterialJson = request.EditorialMaterial is null ? null : JsonSerializer.Serialize(request.EditorialMaterial),
            RequestDigestVersion = request.EditorialMaterial is null ? 1 : 2,
            OriginatingUserId = actor.UserId, RevisionOperationId = request.ClientRequestId,
        };
        dbContext.Submissions.Add(submission);
        dbContext.Revisions.Add(revision);
        try
        {
            // EF commits both inserts as a single relational SaveChanges transaction.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Revisions.Entry(revision).State = EntityState.Detached;
            dbContext.Submissions.Entry(submission).State = EntityState.Detached;
            var winner = await FindRequestAsync(actor.TenantId, actor.PartyId, request.ClientRequestId, cancellationToken);
            if (winner is null)
                throw;
            return await ReplayAsync(winner, digest, cancellationToken);
        }
        return new CreateSubmissionResult(true, Map(submission, revision));
    }

    public async Task<SubmissionReceipt?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(write: false, cancellationToken);
        var submission = await Own(actor.TenantId, actor.PartyId)
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        return submission is null ? null : Map(submission, await RevisionAsync(submission, cancellationToken));
    }

    public async Task<SubmissionPage> ListAsync(int page = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var actor = await RequireActorAsync(write: false, cancellationToken);
        if (page is < 1 or > 10000 || pageSize is < 1 or > 100)
            throw new PublishingException(422, "invalid-page", "Page must be 1–10000 and page size 1–100.");
        var submissions = await Own(actor.TenantId, actor.PartyId)
            .OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var revisionIds = submissions.Select(s => s.CurrentRevisionId).ToList();
        var revisions = await dbContext.Revisions.AsNoTracking()
            .Where(r => r.TenantId == actor.TenantId && revisionIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);
        return new SubmissionPage(page, pageSize,
            submissions.Select(s => Map(s, revisions[s.CurrentRevisionId])).ToList());
    }

    internal async Task<(Guid TenantId, Guid UserId, Guid PartyId)> RequireActorAsync(bool write, CancellationToken cancellationToken)
    {
        if (!user.IsAuthenticated || user.UserId is not { } userId || userId == Guid.Empty)
            throw new PublishingException(401, "unauthenticated", "A signed-in creator is required.");
        var tenantId = tenantProvider.GetCurrentTenantId();
        if (tenantId == Guid.Empty || user.TenantId != tenantId)
            throw new PublishingException(403, "invalid-creator-scope", "Creator scope is unavailable.");
        await moduleGate.EnsureEnabledAsync(tenantId, ModuleIds.Publishing, cancellationToken);
        var partyId = await parties.GetCurrentPartyIdAsync(cancellationToken);
        if (partyId is null || partyId == Guid.Empty)
            throw new PublishingException(403, "no-party", "The signed-in creator must be linked to a party.");
        if (write && !user.Roles.Contains("PersonalUser", StringComparer.Ordinal)
            && !await permissions.HasPermissionAsync(userId, "Publishing.Submit", cancellationToken))
            throw new PublishingException(403, "submit-forbidden", "Creator submission permission is required.");
        return (tenantId, userId, partyId.Value);
    }

    private void RequireChannel(Guid tenantId, CreateSubmissionRequest request)
    {
        if (!options.Value.Channels.TryGetValue(request.ChannelKey, out var channel)
            || !channel.Enabled || channel.TenantId != tenantId || channel.TenantId == Guid.Empty)
            throw new PublishingException(404, "channel-unavailable", "No such submission channel is available.");
        if (channel.MaxPackageBytes <= 0 || string.IsNullOrWhiteSpace(channel.RightsDeclarationVersion))
            throw new PublishingException(503, "channel-not-configured", "The submission channel is not configured for intake.");
        var verifier = options.Value.Verifier;
        if (!PackageCapacity.IsKnown(verifier.CapacityProfile)
            || ((verifier.Provider == "AzureDynamicSessions" || verifier.CapacityProfile is not null)
                && channel.MaxPackageBytes > PackageCapacity.Resolve(verifier.CapacityProfile).PackageBytes))
            throw new PublishingException(503, "channel-not-configured", "The channel exceeds its verifier capacity profile.");
        if (request.DeclaredPackageBytes > channel.MaxPackageBytes)
            throw new PublishingException(413, "package-too-large", "Declared package length exceeds the channel limit.");
        if (!string.Equals(request.RightsDeclarationVersion, channel.RightsDeclarationVersion, StringComparison.Ordinal))
            throw new PublishingException(422, "rights-version-mismatch", "Accept the current distribution permission statement.");
    }

    private IQueryable<PublicationSubmission> Own(Guid tenantId, Guid partyId)
        => dbContext.Submissions.AsNoTracking().Where(s => s.TenantId == tenantId && s.CreatorPartyId == partyId);

    private Task<PublicationSubmission?> FindRequestAsync(Guid tenantId, Guid partyId, Guid requestId, CancellationToken cancellationToken)
        => Own(tenantId, partyId).IncludeSoftDeleted().SingleOrDefaultAsync(s => s.ClientRequestId == requestId, cancellationToken);

    private async Task<CreateSubmissionResult> ReplayAsync(PublicationSubmission submission, string digest, CancellationToken cancellationToken)
    {
        if (submission.IsDeleted)
            throw new PublishingException(409, "request-id-retired", "This client request ID is retired and cannot be reused.");
        // Creation identity survives replacement revisions: compare the original declaration,
        // then return the current receipt rather than reinterpreting the latest revision as the request.
        var original = await dbContext.Revisions.AsNoTracking().SingleAsync(r => r.TenantId == submission.TenantId
            && r.SubmissionId == submission.Id && r.Number == 1, cancellationToken);
        if (!string.Equals(original.RequestDigest, digest, StringComparison.Ordinal))
            throw new PublishingException(409, "request-id-reused", "This client request ID already names different submission material.");
        var current = original.Id == submission.CurrentRevisionId ? original : await RevisionAsync(submission, cancellationToken);
        return new CreateSubmissionResult(false, Map(submission, current));
    }

    private Task<SubmissionRevision> RevisionAsync(PublicationSubmission submission, CancellationToken cancellationToken)
        => dbContext.Revisions.AsNoTracking().SingleAsync(r => r.TenantId == submission.TenantId
            && r.SubmissionId == submission.Id && r.Id == submission.CurrentRevisionId, cancellationToken);

    internal static SubmissionReceipt Map(PublicationSubmission submission, SubmissionRevision revision)
        => new(submission.Id, submission.ClientRequestId, submission.ChannelKey, submission.Status, submission.CreatedAt,
            new SubmissionRevisionResponse(revision.Id, revision.Number, revision.RequestDigest, revision.RequestDigestVersion,
                revision.Title, revision.CreatorDisplayName, revision.Summary, revision.Language, revision.WorldId,
                revision.Edition, revision.PackageFileName, revision.DeclaredPackageBytes, revision.DeclaredPackageSha256,
                revision.RightsDeclarationVersion, revision.DistributionPermissionGranted, PackageVerified: revision.PackageVerifiedAt.HasValue,
                revision.EditorialMaterialJson is null ? null : JsonSerializer.Deserialize<PublicationEditorialMaterial>(revision.EditorialMaterialJson)),
            Convert.ToBase64String(submission.RowVersion));
}
