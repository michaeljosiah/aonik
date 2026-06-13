using Aonik.Documents.Contracts;

namespace Aonik.Documents.Services;

/// <summary>
/// Consumer-facing CRUD over a document's links (Spec 046). Owner-scoped: a
/// caller may only manage links on their own documents. Returns null when the
/// document is not owned by the caller (surfaced as 404, never revealing existence).
/// </summary>
internal interface IDocumentLinkService
{
    Task<IReadOnlyList<DocumentLinkDto>?> ListLinksAsync(Guid documentId, CancellationToken cancellationToken = default);

    Task<DocumentLinkDto?> AddLinkAsync(Guid documentId, string targetType, Guid targetId, CancellationToken cancellationToken = default);

    Task<bool> RemoveLinkAsync(Guid documentId, Guid linkId, CancellationToken cancellationToken = default);
}
