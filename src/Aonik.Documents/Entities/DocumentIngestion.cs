using Aonik.SharedKernel.Primitives;

namespace Aonik.Documents.Entities;

/// <summary>
/// One RAG ingestion run for a document file: the record of embedding a file's
/// extracted text into the vector store (Spec 035 §9). Anemic; all logic lives in
/// the ingestion pipeline (Phase 3). <see cref="DocumentId"/> / <see cref="DocumentFileId"/>
/// are cross-aggregate references stored as plain Guids.
/// </summary>
public class DocumentIngestion : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentFileId { get; set; }

    /// <summary>Qdrant collection the chunks were upserted into.</summary>
    public string VectorCollection { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public string? EmbeddingModel { get; set; }
    public decimal EmbeddingCost { get; set; }

    /// <summary>Pending, Running, Succeeded, Failed.</summary>
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>The embedding run is auditable as an AiRun.</summary>
    public Guid? AiRunId { get; set; }
}
