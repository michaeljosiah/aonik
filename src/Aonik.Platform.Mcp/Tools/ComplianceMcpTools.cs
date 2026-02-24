using System.ComponentModel;
using Aonik.Platform.Contracts.Models.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.SharedKernel.Abstractions;
using ModelContextProtocol.Server;

namespace Aonik.Platform.Mcp.Tools;

/// <summary>
/// MCP tools for compliance and document management operations.
/// Read-only tools for agent reasoning; mutating operations go through proposal pattern.
/// Domain services are injected via DI into method parameters.
/// </summary>
[McpServerToolType]
public static class ComplianceMcpTools
{
    [McpServerTool(Name = "platform_list_documents"), Description("Lists compliance documents with optional filtering by type, status, owner, country, or search term. Returns a paged result.")]
    public static async Task<PagedResult<DocumentListItem>> ListDocuments(
        IDocumentService documentService,
        [Description("Page number (1-based, default 1)")] int pageNumber = 1,
        [Description("Page size (default 20)")] int pageSize = 20,
        [Description("Filter by document type (e.g. Passport, UtilityBill)")] string? documentType = null,
        [Description("Filter by status (e.g. Pending, Verified, Rejected)")] string? status = null,
        [Description("Filter by owner party ID")] Guid? ownerPartyId = null,
        [Description("Filter by country code (e.g. NG, US, GB)")] string? countryCode = null,
        [Description("Search by reference number or other fields")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ListDocumentsRequest(
            pageNumber, pageSize, documentType, status, ownerPartyId, countryCode,
            IssuedFrom: null, IssuedTo: null, ExpiresFrom: null, ExpiresTo: null,
            Tag: null, UsagePurpose: null, Search: search);
        return await documentService.ListDocumentsAsync(request, cancellationToken);
    }

    [McpServerTool(Name = "platform_get_document"), Description("Retrieves full details of a compliance document by its ID, including files, usages, and verification history.")]
    public static async Task<DocumentDetailsResponse?> GetDocument(
        IDocumentService documentService,
        [Description("The unique identifier (GUID) of the document to retrieve")] Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await documentService.GetDocumentAsync(documentId, cancellationToken);
    }
}
