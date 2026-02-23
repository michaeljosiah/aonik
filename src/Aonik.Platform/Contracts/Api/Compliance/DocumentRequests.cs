namespace Aonik.Platform.Contracts.Api.Compliance;

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

public record AddDocumentFileRequest(
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

public record AddDocumentUsageRequest(
    Guid OwnerPartyId,
    string Purpose,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    string? Status,
    string? Notes);

public record AddDocumentVerificationRequest(
    string Decision,
    string? DecisionReasonCode,
    string? DecisionNotes,
    string VerifierType,
    string? VerifierId,
    Guid? AiRunId);
