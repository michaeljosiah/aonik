using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.SharedKernel.Abstractions.Documents;

public sealed record FinancialDocumentLine(string Description, decimal Quantity, decimal UnitPrice, decimal Total);
public sealed record FinancialDocumentFields(
    string Kind, string InvoiceNumber, string CounterpartyName, string Currency, DateTime? DueUtc,
    IReadOnlyList<FinancialDocumentLine> Lines, decimal Subtotal, decimal Tax, decimal Discount, decimal Total,
    double Confidence, IReadOnlyList<string> Evidence);
public sealed record ParsedFinancialDocument(FinancialDocumentFields Fields, Guid AiRunId, string ModelId, string PromptHash);

/// <summary>AI only extracts evidence. Domain validation and human approval remain separate.</summary>
public interface IFinancialDocumentParser
{
    Task<ParsedFinancialDocument> ParseAsync(Guid documentId, Guid fileId, string sourceHash, string text,
        CancellationToken cancellationToken = default);
}

public sealed record FinancialDocumentSource(Guid ConnectionId, string ProviderAccountId, Guid WorkflowId,
    Guid WorkflowVersionId, Guid WorkflowGrantId, Guid AutomationIdentityId, Guid BindingId, int BindingRevision,
    string MessageId, string AttachmentId, string ContentHash);
public interface IFinancialDocumentSourceAccess
{
    Task RequireAsync(FinancialDocumentSource source, WorkflowActor actor, CancellationToken cancellationToken = default);
}

public sealed record FinancialDocumentCandidate(
    Guid ExtractionId, Guid DocumentId, Guid FileId, string SourceHash, string CandidateHash, string Status,
    FinancialDocumentFields Fields, IReadOnlyList<string> Issues, Guid AiRunId, string ModelId, string PromptHash,
    DocumentTextProvenance? Extractor, FinancialDocumentSource? Source, WorkflowActor? ExtractedBy = null);
public interface IFinancialDocumentReader
{
    Task<IReadOnlyList<FinancialDocumentExtractionReceipt>> ListAsync(Guid documentId, WorkflowActor actor,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("Financial candidate listing is unavailable.");
    Task<FinancialDocumentFileContent> OpenFileAsync(Guid documentId, Guid fileId, WorkflowActor actor,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("Private financial source reading is unavailable.");
    Task<FinancialDocumentCandidate> RequireAsync(Guid extractionId, WorkflowActor actor,
        CancellationToken cancellationToken = default);
    Task<FinancialDocumentCandidate> RequireContinuationAsync(Guid extractionId, WorkflowActor actor,
        CancellationToken cancellationToken = default) => RequireAsync(extractionId, actor, cancellationToken);
}
public sealed record FinancialDocumentFileContent(Stream Content, string FileName, string ContentType);

public sealed record ExtractFinancialDocumentArguments(Guid DocumentId, Guid FileId);
public sealed record FinancialDocumentExtractionReceipt(Guid ExtractionId, Guid DocumentId, string CandidateHash,
    string Status, IReadOnlyList<string> Issues, Guid AiRunId);
public sealed record CreateInvoiceFromDocumentArguments(Guid ExtractionId, string CandidateHash, Guid CustomerId, Guid? OrderId = null);
public sealed record FinancialDocumentInvoiceReceipt(Guid InvoiceId, Guid ExtractionId, Guid ApprovalRequestId,
    Guid OperationId, Guid AiRunId, string Status);

public static class FinancialDocumentActions
{
    public const string Extract = "documents.financial.extract";
    public const string ExtractTool = "documents_extract_financial_candidate";
    public const string CreateInvoice = "finance.invoices.create_from_document";
    public const string CreateInvoiceTool = "finance_create_invoice_from_document";
    public const string UseCase = "financial_document_extraction_v1";
    public const string SubjectType = "Document";
    public const string FactSchema = "financial-document.v1";
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };
}
