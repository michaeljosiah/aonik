using System.ComponentModel;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Platform.Agents.Tools;

/// <summary>
/// AI agent tools for document management. Spec 035 — generic documents are owned by
/// <c>Aonik.Documents</c> and read here through the SharedKernel <see cref="IDocumentReader"/>
/// contract (no project reference). Read-only; safe for autonomous use.
/// </summary>
internal sealed class ComplianceTools
{
    private readonly IDocumentReader _documentReader;

    private ComplianceTools(IDocumentReader documentReader) => _documentReader = documentReader;

    [Description("Lists documents with optional filtering by type, status, owner, or search text. Returns a paged result.")]
    public async Task<PagedResult<DocumentListItem>> ListDocuments(
        [Description("Page number (1-based, default 1)")] int pageNumber = 1,
        [Description("Page size (default 20)")] int pageSize = 20,
        [Description("Filter by document type (e.g. Passport, UtilityBill)")] string? documentType = null,
        [Description("Filter by status (e.g. Draft, Submitted, Verified, Rejected)")] string? status = null,
        [Description("Filter by owner party ID")] Guid? ownerPartyId = null,
        [Description("Search by document type, reference number, or issuer")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = new ListDocumentsQuery(
            PageNumber: pageNumber,
            PageSize: pageSize,
            OwnerPartyId: ownerPartyId,
            DocumentType: documentType,
            Status: status,
            Classification: null,
            Tag: null,
            Search: search);
        return await _documentReader.ListDocumentsAsync(query, cancellationToken);
    }

    [Description("Retrieves a document's metadata by its ID.")]
    public async Task<DocumentDto?> GetDocument(
        [Description("The unique identifier (GUID) of the document to retrieve")] Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await _documentReader.GetDocumentAsync(documentId, cancellationToken);
    }

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all document tools.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new ComplianceTools(serviceProvider.GetRequiredService<IDocumentReader>());

        yield return AIFunctionFactory.Create(tools.ListDocuments, name: "platform_list_documents");
        yield return AIFunctionFactory.Create(tools.GetDocument, name: "platform_get_document");
    }
}
