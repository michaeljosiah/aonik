namespace Aonik.Cli.Models;

// ── Document linking API contracts (Spec 046) ───────────────────────
// DocumentListItemDto models the API's enum fields as strings (the API
// serializes enums as strings), so the CLI deserializes without a converter.

public sealed record DocumentLinkDto(
    Guid Id,
    Guid DocumentId,
    string TargetType,
    Guid TargetId,
    DateTime CreatedAt);

public sealed record AddDocumentLinkRequest(
    string TargetType,
    Guid TargetId);

public sealed record DocumentListItemDto(
    Guid DocumentId,
    Guid OwnerPartyId,
    string DocumentType,
    string Classification,
    string Status,
    string IndexStatus,
    DateTime? IssuedOn,
    DateTime? ExpiresOn,
    int FilesCount,
    DateTime CreatedAt);

public sealed record ListDocumentsOptions(
    Guid? CareEntityId,
    string? DocumentType,
    int? Year,
    int Page,
    int PageSize,
    OutputMode OutputMode);
