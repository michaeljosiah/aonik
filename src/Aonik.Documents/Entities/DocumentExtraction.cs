using Aonik.SharedKernel.Primitives;

namespace Aonik.Documents.Entities;

/// <summary>
/// OCR / structured-extraction output for a document (Spec 035 §9). Carries the
/// <see cref="AiRunId"/> so an AI-assisted extraction is auditable. Anemic.
/// </summary>
public class DocumentExtraction : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }

    /// <summary>OcrText or StructuredFields.</summary>
    public string ExtractionType { get; set; } = string.Empty;
    public string OutputJson { get; set; } = "{}";
    public double? Confidence { get; set; }

    /// <summary>The extraction run is auditable as an AiRun.</summary>
    public Guid? AiRunId { get; set; }
}
