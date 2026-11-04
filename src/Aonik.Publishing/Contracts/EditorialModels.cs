using System.Text.Json.Serialization;

namespace Aonik.Publishing.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublicationEditorialMaterial(string Audience, string AgeRating, string ReleaseKind,
    string ArtworkAssetId, string Attribution, IReadOnlyList<PublicationRightsEvidence> RightsEvidence);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublicationRightsEvidence(string RightsBasis, string EvidenceReference, IReadOnlyList<string> AssetIds);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditorialApprovalChecks(bool DistributionRightsReviewed, bool AttributionReviewed, bool AudienceSafetyReviewed);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EditorialDecisionRequest(Guid OperationId, Guid RevisionId, string ReviewDigest,
    string ExpectedVersion, string Decision, string InternalReason, string CreatorMessage, EditorialApprovalChecks? Checks = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceSubmissionRevisionRequest(Guid OperationId, Guid ExpectedRevisionId,
    string ExpectedVersion, CreateSubmissionRequest Declaration);

public sealed record EditorialDecisionResponse(Guid Id, Guid OperationId, Guid RevisionId, string ReviewDigest,
    string Decision, string InternalReason, string CreatorMessage, Guid ActorUserId, Guid ActorPartyId,
    string PolicyVersion, DateTime DecidedAt, EditorialApprovalChecks? Checks, bool IsActive = false);
public sealed record CreatorDecisionSummary(Guid Id, Guid RevisionId, string Decision, string Message, DateTime DecidedAt);
public sealed record EditorialSnapshotResponse(Guid Id, string Digest, int DigestVersion, Guid ValidationReportId,
    string MaterialJson, DateTime CreatedAt);
public sealed record EditorialReviewDetail(SubmissionReceipt Submission, ValidationPreview? Validation,
    EditorialSnapshotResponse? Snapshot, string? BlockingCode, string? ApprovalBlockingCode,
    IReadOnlyList<EditorialDecisionResponse> Decisions, IReadOnlyList<EditorialActivity>? Activity = null, bool PackageAvailable = false);
public sealed record EditorialActivity(string Kind, string Status, DateTime At, string Description);
public sealed record EditorialReviewPage(int Page, int PageSize, IReadOnlyList<SubmissionReceipt> Items, int TotalCount = 0);
public sealed record CreatorDecisionPage(int Page, int PageSize, IReadOnlyList<CreatorDecisionSummary> Items);

public interface IEditorialReviewService
{
    Task<EditorialReviewPage> ListAsync(string channelKey, string? status = null, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default, string? query = null);
    Task<EditorialReviewDetail> GetAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<EditorialDecisionResponse> DecideAsync(Guid submissionId, EditorialDecisionRequest request, CancellationToken cancellationToken = default);
    Task<(Stream Content, string FileName)> DownloadAsync(Guid submissionId, Guid revisionId, CancellationToken cancellationToken = default);
    Task<(Stream Content, string MediaType)> ImageAsync(Guid submissionId, Guid revisionId, string assetId, CancellationToken cancellationToken = default);
}

public interface ISubmissionRevisionService
{
    Task<SubmissionReceipt> ReplaceAsync(Guid submissionId, ReplaceSubmissionRevisionRequest request, CancellationToken cancellationToken = default);
    Task<CreatorDecisionPage> DecisionsAsync(Guid submissionId, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
