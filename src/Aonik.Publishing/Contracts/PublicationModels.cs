using System.Text.Json.Serialization;

using Aonik.Publishing.Entities;

namespace Aonik.Publishing.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishRevisionRequest(Guid OperationId, Guid RevisionId, Guid DecisionId, string ReviewDigest,
    string ExpectedVersion, long ExpectedSerial, string? ExpectedBaseFingerprint, string? ExpectedSlotFingerprint, bool ConfirmBaseReplacement = false, string Reason = "");
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublicationActionRequest(Guid OperationId, string ExpectedVersion, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InitialisePublicationChannelRequest(Guid OperationId, string BaselineSha256, string Reason);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetPublicationHoldRequest(Guid OperationId, string ExpectedVersion, string Reason, bool Hold);

public sealed record PublicationOperationResponse(Guid Id, string ChannelKey, string Kind, Guid? SubmissionId, Guid? RevisionId,
    Guid? DecisionId, Guid? EditionId, string Status, string Code, int Attempts, long? CommittedSerial, DateTime CreatedAt,
    DateTime? CompletedAt, string Reason, string Version, bool CanRetry);
public sealed record PublishedEditionResponse(Guid Id, string WorldId, string Slot, string Kind, string Title, Guid? SubmissionId,
    Guid? RevisionId, Guid? DecisionId, string PackageSha256, string? Fingerprint, bool IsLegacy, bool IsCurrent, DateTime? WithdrawnAt, string Version, string PinJson, long PublishedSerial);
public sealed record PublicationChannelResponse(string Key, bool CanReview, bool CanPublish, bool CanWithdraw, bool DeliveryEnabled,
    bool PublicEnabled, bool Initialised, long? Serial, string? Version, string? MirrorStatus, string? MirrorCode, long? MirroredSerial,
    string? ReadinessCode, string BaselineSha256, string? PublicCatalogueUrl);
public sealed record PublicationCatalogueResponse(PublicationChannelResponse Channel, int Page, int PageSize, int TotalEditions, IReadOnlyList<PublishedEditionResponse> Editions,
    IReadOnlyList<PublicationOperationResponse> Operations, IReadOnlyList<ApprovedReleaseSummary> Approved);
public sealed record PublicRevisionMetadata(Guid Id, int Number, string Title, string CreatorDisplayName, string Edition, string PackageSha256,
    long PackageBytes, string? Audience, string? AgeRating, string? Kind);
public sealed record ApprovedReleaseSummary(Guid ReceiptId, PublicRevisionMetadata Metadata, string Status, string Version, string? HoldReason);
public sealed record PublicationReleasePreview(Guid SubmissionId, Guid RevisionId, Guid? DecisionId, string Status, string ReviewDigest,
    string Version, string? HoldReason, string? BlockingCode, long? Serial, string? CurrentBaseFingerprint, string? CurrentSlotFingerprint,
    string? PublicWorldJson, IReadOnlyList<PublicationOperationResponse> Operations, PublicRevisionMetadata Metadata);

public interface IPublicationService
{
    Task<IReadOnlyList<PublicationChannelResponse>> ChannelsAsync(CancellationToken cancellationToken = default);
    Task<PublicationCatalogueResponse> CatalogueAsync(string channelKey, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PublicationReleasePreview> PreviewAsync(Guid submissionId, CancellationToken cancellationToken = default);
    Task<PublicationOperationResponse> InitialiseAsync(string channelKey, InitialisePublicationChannelRequest request, CancellationToken cancellationToken = default);
    Task<PublicationOperationResponse> PublishAsync(Guid submissionId, PublishRevisionRequest request, CancellationToken cancellationToken = default);
    Task<PublicationOperationResponse> RetryAsync(Guid operationId, PublicationActionRequest request, CancellationToken cancellationToken = default);
    Task<PublicationOperationResponse> WithdrawAsync(Guid editionId, PublicationActionRequest request, CancellationToken cancellationToken = default);
    Task<PublicationOperationResponse> HoldAsync(Guid submissionId, SetPublicationHoldRequest request, CancellationToken cancellationToken = default);
    Task<PublicationOperationResponse> RetryMirrorAsync(string channelKey, PublicationActionRequest request, CancellationToken cancellationToken = default);
}
public interface IPublicationArtifactStore
{
    string LocationId { get; }
    Task WriteAsync(PublicationArtifact artifact, Stream source, CancellationToken cancellationToken = default);
    Task<Stream> OpenAsync(PublicationArtifact artifact, CancellationToken cancellationToken = default);
    Task VerifyPublicAsync(PublicationArtifact artifact, CancellationToken cancellationToken = default);
}
public sealed record LegacyMirrorHead(string? Revision, string? Json);
public interface ILegacyCatalogueMirror
{
    Task<LegacyMirrorHead> ReadAsync(Services.PublishingChannelOptions channel, CancellationToken cancellationToken = default);
    Task CompareAndSwapAsync(Services.PublishingChannelOptions channel, string? expectedRevision, string json, CancellationToken cancellationToken = default);
}
