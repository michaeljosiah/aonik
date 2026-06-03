using System.ComponentModel;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using ModelContextProtocol.Server;

namespace Aonik.Platform.Mcp.Tools;

/// <summary>
/// MCP tools for document management (Spec 035 — generic documents are read through the
/// SharedKernel <see cref="IDocumentReader"/> contract). Read-only tools for agent reasoning;
/// mutating operations go through the proposal pattern. Services are injected via DI per method.
/// </summary>
[McpServerToolType]
public static class ComplianceMcpTools
{
    [McpServerTool(Name = "platform_list_documents"), Description("Lists documents with optional filtering by type, status, owner, or search term. Returns a paged result.")]
    public static async Task<PagedResult<DocumentListItem>> ListDocuments(
        IDocumentReader documentReader,
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
        return await documentReader.ListDocumentsAsync(query, cancellationToken);
    }

    [McpServerTool(Name = "platform_get_document"), Description("Retrieves a document's metadata by its ID.")]
    public static async Task<DocumentDto?> GetDocument(
        IDocumentReader documentReader,
        [Description("The unique identifier (GUID) of the document to retrieve")] Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await documentReader.GetDocumentAsync(documentId, cancellationToken);
    }
}
