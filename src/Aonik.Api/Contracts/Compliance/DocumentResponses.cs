namespace Aonik.Api.Contracts.Compliance;

public record DocumentResponse(
    Guid DocumentId,
    Guid OwnerPartyId,
    string DocumentType,
    string Status,
    DateTime? IssuedOn,
    DateTime? ExpiresOn,
    string? IssuerName,
    string? CountryCode,
    string? ReferenceNumber,
    IReadOnlyList<string> Tags,
    string AttributesJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record DocumentFileResponse(
    Guid DocumentFileId,
    Guid DocumentId,
    string StorageProvider,
    string? StorageContainer,
    string StorageKey,
    string ContentType,
    string? FileName,
    long? FileSizeBytes,
    string? Sha256,
    int? PageIndex,
    string? Side,
    DateTime? CapturedAt,
    string? CapturedBy,
    string MetadataJson,
    DateTime CreatedAt);

public record DocumentVerificationResponse(
    Guid DocumentVerificationId,
    Guid DocumentUsageId,
    string Decision,
    string? DecisionReasonCode,
    string? DecisionNotes,
    string VerifierType,
    string? VerifierId,
    Guid? AiRunId,
    DateTime CreatedAt);

public record DocumentUsageResponse(
    Guid DocumentUsageId,
    Guid DocumentId,
    Guid OwnerPartyId,
    string Purpose,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    string Status,
    Guid? VerifiedByUserId,
    DateTime? VerifiedAt,
    string? Notes,
    IReadOnlyList<DocumentVerificationResponse> Verifications,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record DocumentVersionResponse(
    Guid DocumentVersionId,
    Guid DocumentId,
    int Version,
    string Status,
    DateTime? SubmittedAt,
    DateTime? DecisionedAt,
    string? DecisionReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record DocumentDetailsResponse(
    DocumentResponse Document,
    IReadOnlyList<DocumentFileResponse> Files,
    IReadOnlyList<DocumentUsageResponse> Usages,
    IReadOnlyList<DocumentVersionResponse> Versions);
