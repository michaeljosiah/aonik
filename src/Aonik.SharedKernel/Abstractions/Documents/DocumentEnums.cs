namespace Aonik.SharedKernel.Abstractions.Documents;

/// <summary>
/// Sensitivity classification for a document. Drives both the RAG indexing decision
/// (see <see cref="DocumentIndexStatus"/>) and the retrieval scope enforced by
/// <see cref="IDocumentSearch"/>. Assigned at upload (defaulted from document type) and
/// optionally refined by an AI classifier that records an <c>AiRun</c>.
/// See <a href="../../../docs/specifications/035.extract-documents-module.html">Spec 035 §10</a>.
/// </summary>
public enum DocumentClassification
{
    /// <summary>Public / tenant-wide content (e.g. product terms). Indexed; tenant scope.</summary>
    Public = 0,

    /// <summary>Operational / internal content. Indexed; tenant scope.</summary>
    Internal = 1,

    /// <summary>Personal to a party (tax return, payslip, statement). Indexed; tenant + owner-party scope.</summary>
    Personal = 2,

    /// <summary>Sensitive evidence (ID scans, proof-of-address images). Metadata-only until OCR + redaction; tenant + owner-party + purpose scope.</summary>
    Sensitive = 3,

    /// <summary>Explicitly excluded from indexing. Never embedded; direct read only.</summary>
    Restricted = 4,
}

/// <summary>
/// Lifecycle of a document's presence in the vector store.
/// </summary>
public enum DocumentIndexStatus
{
    /// <summary>Classification forbids indexing (e.g. <see cref="DocumentClassification.Restricted"/>).</summary>
    NotIndexable = 0,

    /// <summary>Eligible for indexing; awaiting the async ingestion pipeline.</summary>
    Pending = 1,

    /// <summary>Chunks embedded and upserted; searchable.</summary>
    Indexed = 2,

    /// <summary>Ingestion failed after retries; see the owning <c>DocumentIngestion</c> record.</summary>
    Failed = 3,
}
