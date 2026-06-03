namespace Aonik.SharedKernel.Abstractions.Documents;

// ── Cross-module document DTOs and commands ─────────────────────────────────
// Consumers (Finance, PersonalFinance, Platform/Compliance, Ai, Agents) talk to
// the Documents module exclusively through these shapes — never through the
// Aonik.Documents entity types. DTOs carry only what consumers actually use.
// See docs/specifications/035.extract-documents-module.html.

/// <summary>A document's metadata view (the generic evidence record, no compliance fields).</summary>
public sealed record DocumentDto(
    Guid DocumentId,
    Guid OwnerPartyId,
    string DocumentType,
    DocumentClassification Classification,
    string Status,
    string Source,
    DocumentIndexStatus IndexStatus,
    DateTime? IndexedAt,
    DateTime? IssuedOn,
    DateTime? ExpiresOn,
    string? IssuerName,
    string? CountryCode,
    string? ReferenceNumber,
    IReadOnlyList<string> Tags,
    string AttributesJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>A physical file attached to a document (blob reference + metadata).</summary>
public sealed record DocumentFileDto(
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
    DateTime CreatedAt);

/// <summary>Lightweight list projection for paged document listings.</summary>
public sealed record DocumentListItem(
    Guid DocumentId,
    Guid OwnerPartyId,
    string DocumentType,
    DocumentClassification Classification,
    string Status,
    DocumentIndexStatus IndexStatus,
    DateTime? IssuedOn,
    DateTime? ExpiresOn,
    int FilesCount,
    DateTime CreatedAt);

/// <summary>Filter/paging query for <see cref="IDocumentReader.ListDocumentsAsync"/>. Tenant is resolved from ambient context inside the implementation.</summary>
public sealed record ListDocumentsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? OwnerPartyId = null,
    string? DocumentType = null,
    string? Status = null,
    DocumentClassification? Classification = null,
    string? Tag = null,
    string? Search = null);

/// <summary>Command to create a generic document. Classification defaults from <see cref="DocumentType"/> when omitted.</summary>
public sealed record CreateDocumentCommand(
    Guid OwnerPartyId,
    string DocumentType,
    DocumentClassification? Classification = null,
    string? Status = null,
    string? Source = null,
    DateTime? IssuedOn = null,
    DateTime? ExpiresOn = null,
    string? IssuerName = null,
    string? CountryCode = null,
    string? ReferenceNumber = null,
    IReadOnlyList<string>? Tags = null,
    string? AttributesJson = null);

/// <summary>Command to upload a file's bytes into an existing document. The stream is passed separately to <see cref="IDocumentWriter.UploadFileAsync"/>.</summary>
public sealed record UploadFileCommand(
    Guid DocumentId,
    string FileName,
    string ContentType,
    int? PageIndex = null,
    string? Side = null,
    DateTime? CapturedAt = null,
    string? CapturedBy = null,
    string? MetadataJson = null);

/// <summary>A retrieval hit: one indexed chunk of a document matching a scoped search.</summary>
public sealed record DocumentChunkHit(
    Guid DocumentId,
    int ChunkIndex,
    string Content,
    double Score,
    string DocumentType,
    Guid OwnerPartyId);

/// <summary>
/// The mandatory scope for every document search. There is no unscoped overload.
/// <see cref="OwnerPartyId"/> is required for <see cref="DocumentClassification.Personal"/> /
/// <see cref="DocumentClassification.Sensitive"/> retrieval; callers must derive it from
/// authenticated context (tenant provider + current user / owner party), never from model input.
/// Tenant isolation is always applied beneath this scope by the vector store, fail-closed.
/// </summary>
public sealed record DocumentSearchScope(
    Guid TenantId,
    Guid? OwnerPartyId = null,
    IReadOnlyList<string>? Purposes = null,
    IReadOnlyList<DocumentClassification>? Classifications = null);

/// <summary>Request to (re)index a document's text chunks into the vector store. Issued by the ingestion pipeline.</summary>
public sealed record DocumentIndexRequest(
    Guid DocumentId,
    Guid OwnerPartyId,
    DocumentClassification Classification,
    string DocumentType,
    string? Purpose,
    IReadOnlyList<string> Chunks);

/// <summary>
/// Outcome of an indexing run. Surfaces the embedding model and a cost estimate up from the
/// Infrastructure embedding layer so the ingestion pipeline can record them on its
/// <c>DocumentIngestion</c> audit row (Spec 035 §9 / R9) — the Documents module never sees the
/// embedding service directly. <see cref="ChunkCount"/> is zero for a skip (Restricted/Sensitive)
/// or an explicit empty-chunk purge.
/// </summary>
public sealed record DocumentIndexResult(
    int ChunkCount,
    string EmbeddingModel,
    decimal EstimatedCost);
