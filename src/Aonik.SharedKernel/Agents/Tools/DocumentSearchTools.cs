using System.ComponentModel;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.SharedKernel.Agents.Tools;

/// <summary>
/// Cross-cutting, read-only AITool that semantically searches the authenticated user's own
/// documents mid-conversation (Spec 035 §13). Lives on SharedKernel so any domain agent can offer
/// it without a back-pointing reference, mirroring <see cref="UserMemoryRecallTools"/>.
/// <para>
/// The retrieval scope — tenant + owner party — is derived entirely from authenticated context
/// (<see cref="ITenantProvider"/> + <see cref="ICurrentUserProvider"/> → <see cref="IUserPartyResolver"/>),
/// never from model input, so a prompt cannot widen its own scope across parties (Spec 035 R7).
/// Tenant isolation is enforced fail-closed beneath the scope by the vector store; an unlinked user
/// resolves to no owner party, which keeps results tenant-wide (Public/Internal) rather than
/// surfacing anyone's personal documents. The tool self-disables when its backends are unregistered.
/// </para>
/// </summary>
public sealed class DocumentSearchTools
{
    private readonly IDocumentSearch _documentSearch;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUserPartyResolver _userPartyResolver;

    private DocumentSearchTools(
        IDocumentSearch documentSearch,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IUserPartyResolver userPartyResolver)
    {
        _documentSearch = documentSearch;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _userPartyResolver = userPartyResolver;
    }

    [Description(
        "Search the signed-in user's own uploaded documents (tax returns, bank statements, payslips, " +
        "letters, contracts) for passages relevant to a natural-language query. Use this to answer " +
        "questions like 'what did my last tax return say' or 'how much was my electricity bill'. Only " +
        "the current user's documents are searched. Returns ranked passages with relevance scores, or " +
        "empty if nothing relevant is found.")]
    public async Task<IReadOnlyList<DocumentSearchToolHit>> SearchMyDocuments(
        [Description("Natural-language description of what to find in the user's documents")]
        string query,
        [Description("Maximum number of passages to return (1-20, default 8)")]
        int limit = 8,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<DocumentSearchToolHit>();
        }

        // Fail closed: no authenticated tenant/user → no search (never widen).
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId) || tenantId == Guid.Empty)
        {
            return Array.Empty<DocumentSearchToolHit>();
        }

        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            return Array.Empty<DocumentSearchToolHit>();
        }

        // Owner party is resolved from auth context — never accepted as a model argument (R7).
        var ownerPartyId = await _userPartyResolver.GetPartyIdForUserAsync(tenantId, userId, cancellationToken);

        var scope = new DocumentSearchScope(tenantId, ownerPartyId);
        var hits = await _documentSearch.SearchAsync(
            query, scope, Math.Clamp(limit, 1, 20), cancellationToken);

        // Project to the model-facing shape; the owner party id is intentionally omitted (the scope
        // already constrains it, and it must not be surfaced to the LLM).
        return hits
            .Select(h => new DocumentSearchToolHit(h.DocumentId, h.DocumentType, h.ChunkIndex, h.Content, h.Score))
            .ToList();
    }

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var documentSearch = serviceProvider.GetService<IDocumentSearch>();
        var tenantProvider = serviceProvider.GetService<ITenantProvider>();
        var currentUserProvider = serviceProvider.GetService<ICurrentUserProvider>();
        var userPartyResolver = serviceProvider.GetService<IUserPartyResolver>();

        // If any dependency is missing (e.g. no vector backend wired), skip — tool unavailable.
        if (documentSearch is null || tenantProvider is null
            || currentUserProvider is null || userPartyResolver is null)
        {
            yield break;
        }

        var tools = new DocumentSearchTools(
            documentSearch, tenantProvider, currentUserProvider, userPartyResolver);

        yield return AIFunctionFactory.Create(tools.SearchMyDocuments, name: "documents_search");
    }
}

/// <summary>
/// A document search hit shaped for the model (Spec 035 §13). Deliberately excludes the owner party
/// id — the scope already constrains retrieval to the user's party, so it is never surfaced to the LLM.
/// </summary>
public sealed record DocumentSearchToolHit(
    Guid DocumentId,
    string DocumentType,
    int ChunkIndex,
    string Content,
    double Score);
