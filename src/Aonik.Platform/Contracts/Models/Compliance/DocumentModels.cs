namespace Aonik.Platform.Contracts.Models.Compliance;

public record CreateDocumentRequest(
    Guid OwnerPartyId,
    string DocumentType,
    string? Status,
    DateTime? IssuedOn,
    DateTime? ExpiresOn,
    string? IssuerName,
    string? CountryCode,
    string? ReferenceNumber,
    IReadOnlyList<string> Tags,
    string? AttributesJson);

public record ListDocumentsRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? DocumentType = null,
    string? Status = null,
    Guid? OwnerPartyId = null,
    string? CountryCode = null,
    DateTime? IssuedFrom = null,
    DateTime? IssuedTo = null,
    DateTime? ExpiresFrom = null,
    DateTime? ExpiresTo = null,
    string? Tag = null,
    string? UsagePurpose = null,
    string? Search = null,
    string? RelatedEntityType = null,
    Guid? RelatedEntityId = null);

public record DocumentListItem(
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
    int FilesCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

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

public record AddDocumentFileRequest(
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
    string? MetadataJson);

public record UploadDocumentFileRequest(
    Guid DocumentId,
    string FileName,
    string ContentType,
    int? PageIndex,
    string? Side,
    DateTime? CapturedAt,
    string? CapturedBy,
    string? MetadataJson);

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

public record AddDocumentUsageRequest(
    Guid DocumentId,
    Guid OwnerPartyId,
    string Purpose,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    string? Status,
    string? Notes);

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

public record AddDocumentVerificationRequest(
    Guid DocumentUsageId,
    string Decision,
    string? DecisionReasonCode,
    string? DecisionNotes,
    string VerifierType,
    string? VerifierId,
    Guid? AiRunId);

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
