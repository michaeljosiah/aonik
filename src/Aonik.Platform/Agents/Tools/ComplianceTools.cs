using System.ComponentModel;
using Aonik.Platform.Contracts.Models.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Platform.Agents.Tools;

/// <summary>
/// AI agent tools for compliance and document management operations.
/// Read-only tools are safe for autonomous use; mutating tools should go through
/// the proposal pattern at the agent level.
/// </summary>
internal sealed class ComplianceTools
{
    private readonly IDocumentService _documentService;

    private ComplianceTools(IDocumentService documentService) => _documentService = documentService;

    [Description("Lists compliance documents with optional filtering by type, status, owner, country, dates, or tags. Returns a paged result.")]
    public async Task<PagedResult<DocumentListItem>> ListDocuments(
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
        return await _documentService.ListDocumentsAsync(request, cancellationToken);
    }

    [Description("Retrieves full details of a compliance document by its ID, including files, usages, and verification history.")]
    public async Task<DocumentDetailsResponse?> GetDocument(
        [Description("The unique identifier (GUID) of the document to retrieve")] Guid documentId,
        CancellationToken cancellationToken = default)
    {
        return await _documentService.GetDocumentAsync(documentId, cancellationToken);
    }

    /// <summary>
    /// Creates <see cref="AITool"/> instances for all compliance tools.
    /// </summary>
    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new ComplianceTools(serviceProvider.GetRequiredService<IDocumentService>());

        yield return AIFunctionFactory.Create(tools.ListDocuments, name: "platform_list_documents");
        yield return AIFunctionFactory.Create(tools.GetDocument, name: "platform_get_document");
    }
}
