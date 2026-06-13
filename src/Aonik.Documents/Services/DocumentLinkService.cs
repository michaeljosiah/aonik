using Aonik.Documents.Contracts;
using Aonik.Documents.Persistence;
using Aonik.Platform.Entities.Compliance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Documents.Services;

/// <summary>
/// Document linking for Simi's Vault (Spec 046). Implements the cross-module
/// <see cref="IDocumentLinkReader"/> (read by PersonalFinance/Spec 048) and the
/// internal <see cref="IDocumentLinkService"/> CRUD. Owner-scoped on the
/// document's owner party (derived from auth, never request input) — so a link
/// to another user's target can never surface another user's document.
/// </summary>
internal sealed class DocumentLinkService : IDocumentLinkReader, IDocumentLinkService
{
    private static readonly string[] TenantWideRoles =
        { "PlatformAdmin", "TenantAdmin", "Operations", "ReadOnly" };
    private const string CustomerRole = "PersonalUser";

    private readonly DocumentsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserPartyResolver _userPartyResolver;

    public DocumentLinkService(
        DocumentsDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserContext currentUserContext,
        IUserPartyResolver userPartyResolver)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserContext = currentUserContext;
        _userPartyResolver = userPartyResolver;
    }

    // ── IDocumentLinkReader (cross-module) ──────────────────────────────

    public async Task<IReadOnlyList<DocumentRef>> GetForTargetAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var scope = await ResolveCallerScopeAsync(tenantId, cancellationToken);
        if (scope.IsDeniedCustomer)
        {
            return Array.Empty<DocumentRef>();
        }

        var docsQuery =
            from link in _dbContext.DocumentLinks.AsNoTracking()
            join doc in _dbContext.Documents.AsNoTracking() on link.DocumentId equals doc.Id
            where link.TenantId == tenantId && link.TargetType == targetType && link.TargetId == targetId
                && doc.TenantId == tenantId
                && (scope.TenantWide || doc.OwnerPartyId == scope.OwnerPartyId)
            select doc;

        var docs = await docsQuery.Distinct().ToListAsync(cancellationToken);
        return await BuildRefsAsync(docs, tenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentRef>> GetForOwnerTargetAsync(
        Guid ownerUserId,
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (ownerUserId == Guid.Empty)
        {
            return Array.Empty<DocumentRef>();
        }

        // Scope to the OWNER's party (not the caller). The caller is a Circle member whose access
        // was already authorised by the grant before we got here (Spec 048); this returns only the
        // owner's documents for the target, refs only.
        var ownerPartyId = await _userPartyResolver.GetPartyIdForUserAsync(tenantId, ownerUserId, cancellationToken);
        if (ownerPartyId is null)
        {
            return Array.Empty<DocumentRef>();
        }

        var docs = await (
            from link in _dbContext.DocumentLinks.AsNoTracking()
            join doc in _dbContext.Documents.AsNoTracking() on link.DocumentId equals doc.Id
            where link.TenantId == tenantId && link.TargetType == targetType && link.TargetId == targetId
                && doc.TenantId == tenantId && doc.OwnerPartyId == ownerPartyId
            select doc)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await BuildRefsAsync(docs, tenantId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountForEntitiesAsync(
        IReadOnlyList<Guid> careEntityIds,
        CancellationToken cancellationToken = default)
    {
        if (careEntityIds is null || careEntityIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var scope = await ResolveCallerScopeAsync(tenantId, cancellationToken);
        if (scope.IsDeniedCustomer)
        {
            return new Dictionary<Guid, int>();
        }

        var rows = await (
            from link in _dbContext.DocumentLinks.AsNoTracking()
            join doc in _dbContext.Documents.AsNoTracking() on link.DocumentId equals doc.Id
            where link.TenantId == tenantId && link.TargetType == "careEntity" && careEntityIds.Contains(link.TargetId)
                && doc.TenantId == tenantId
                && (scope.TenantWide || doc.OwnerPartyId == scope.OwnerPartyId)
            select new { link.TargetId, link.DocumentId })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.TargetId)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    // ── IDocumentLinkService (consumer CRUD) ────────────────────────────

    public async Task<IReadOnlyList<DocumentLinkDto>?> ListLinksAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!await OwnsDocumentAsync(tenantId, documentId, cancellationToken))
        {
            return null;
        }

        var links = await _dbContext.DocumentLinks.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.DocumentId == documentId)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        return links.Select(Map).ToList();
    }

    public async Task<DocumentLinkDto?> AddLinkAsync(Guid documentId, string targetType, Guid targetId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!await OwnsDocumentAsync(tenantId, documentId, cancellationToken))
        {
            return null;
        }

        var normalizedType = (targetType ?? string.Empty).Trim();

        // Idempotent: a duplicate (document, type, target) link returns the existing one.
        var existing = await _dbContext.DocumentLinks
            .FirstOrDefaultAsync(
                l => l.TenantId == tenantId && l.DocumentId == documentId
                    && l.TargetType == normalizedType && l.TargetId == targetId,
                cancellationToken);
        if (existing is not null)
        {
            return Map(existing);
        }

        var link = new DocumentLink
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DocumentId = documentId,
            TargetType = normalizedType,
            TargetId = targetId,
        };

        _dbContext.DocumentLinks.Add(link);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(link);
    }

    public async Task<bool> RemoveLinkAsync(Guid documentId, Guid linkId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!await OwnsDocumentAsync(tenantId, documentId, cancellationToken))
        {
            return false;
        }

        var link = await _dbContext.DocumentLinks
            .FirstOrDefaultAsync(
                l => l.Id == linkId && l.TenantId == tenantId && l.DocumentId == documentId,
                cancellationToken);
        if (link is null)
        {
            return false;
        }

        _dbContext.DocumentLinks.Remove(link);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private async Task<bool> OwnsDocumentAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken)
    {
        var scope = await ResolveCallerScopeAsync(tenantId, cancellationToken);
        if (scope.IsDeniedCustomer)
        {
            return false;
        }

        return await _dbContext.Documents.AsNoTracking().AnyAsync(
            d => d.Id == documentId && d.TenantId == tenantId
                && (scope.TenantWide || d.OwnerPartyId == scope.OwnerPartyId),
            cancellationToken);
    }

    private readonly record struct CallerScope(bool TenantWide, Guid? OwnerPartyId)
    {
        public bool IsDeniedCustomer => !TenantWide && OwnerPartyId is null;
    }

    private async Task<CallerScope> ResolveCallerScopeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var roles = _currentUserContext.Roles ?? Array.Empty<string>();
        var isStaff = roles.Any(role => TenantWideRoles.Contains(role, StringComparer.Ordinal));

        if (isStaff || !roles.Contains(CustomerRole, StringComparer.Ordinal))
        {
            return new CallerScope(TenantWide: true, OwnerPartyId: null);
        }

        var userId = _currentUserContext.UserId;
        if (userId is null || userId.Value == Guid.Empty)
        {
            return new CallerScope(TenantWide: false, OwnerPartyId: null);
        }

        var partyId = await _userPartyResolver.GetPartyIdForUserAsync(tenantId, userId.Value, cancellationToken);
        return new CallerScope(TenantWide: false, OwnerPartyId: partyId);
    }

    /// <summary>Materialises document refs (with the first page's file name) for a set of documents.</summary>
    private async Task<IReadOnlyList<DocumentRef>> BuildRefsAsync(
        List<Document> docs, Guid tenantId, CancellationToken cancellationToken)
    {
        if (docs.Count == 0)
        {
            return Array.Empty<DocumentRef>();
        }

        var docIds = docs.Select(d => d.Id).ToList();
        var files = await _dbContext.DocumentFiles.AsNoTracking()
            .Where(f => f.TenantId == tenantId && docIds.Contains(f.DocumentId))
            .OrderBy(f => f.PageIndex)
            .ToListAsync(cancellationToken);

        return docs.Select(d =>
        {
            var fileName = files.FirstOrDefault(f => f.DocumentId == d.Id)?.FileName;
            return new DocumentRef(d.Id, d.Title, d.DocumentType, fileName, ThumbnailUrl: null);
        }).ToList();
    }

    private static DocumentLinkDto Map(DocumentLink l)
        => new(l.Id, l.DocumentId, l.TargetType, l.TargetId, l.CreatedAt);
}
