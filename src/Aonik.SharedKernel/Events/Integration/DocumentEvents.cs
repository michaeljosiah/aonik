namespace Aonik.SharedKernel.Events.Integration;

using Aonik.SharedKernel.Abstractions.Documents;

// ── Documents-originated integration events ─────────────────────────────────
// Published by the Documents module. The Worker (ingestion pipeline) and
// Platform/Compliance subscribe to react to document lifecycle transitions.
// See docs/specifications/035.extract-documents-module.html §11.

/// <summary>
/// Raised when a file is uploaded into a document. The async ingestion pipeline
/// subscribes to extract text, chunk, embed, and upsert into the vector store when the
/// document's <see cref="DocumentClassification"/> permits indexing.
/// </summary>
public record DocumentUploadedEvent(
    Guid TenantId,
    Guid DocumentId,
    Guid DocumentFileId,
    Guid OwnerPartyId,
    DocumentClassification Classification,
    string ContentType) : ITenantScopedEvent;

/// <summary>
/// Raised when a document's chunks have been embedded and become searchable.
/// Consumers can react when a document transitions to searchable state.
/// </summary>
public record DocumentIndexedEvent(
    Guid TenantId,
    Guid DocumentId,
    int ChunkCount) : ITenantScopedEvent;

/// <summary>
/// Raised when a document is deleted. Triggers vector purge and Compliance orphan
/// handling (dependent usages are marked Expired, never silently deleted), and feeds
/// the user-lifecycle closure flow (Spec 026) so a party's documents are erased on closure.
/// </summary>
public record DocumentDeletedEvent(
    Guid TenantId,
    Guid DocumentId,
    Guid OwnerPartyId) : ITenantScopedEvent;
