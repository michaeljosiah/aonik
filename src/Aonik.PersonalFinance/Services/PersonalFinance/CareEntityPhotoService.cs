using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

/// <summary>
/// Banner-image upload/removal for <c>CareEntity</c> (Spec 049 Part B). Composes the SharedKernel
/// Documents contracts: an image is stored as an owner-scoped, <c>Personal</c>-classified
/// <c>Document</c> of type <c>banner</c>, the entity's <c>PhotoDocumentId</c> is pointed at it, and
/// a replaced/removed banner is erased so no orphan blob persists. The banner is referenced solely
/// via <c>PhotoDocumentId</c> — it is not a Spec 046 <c>DocumentLink</c>, so it never appears in the
/// profile's documents (vault) list. The enriched response (with a signed <c>PhotoUrl</c>) is
/// produced by delegating to <see cref="ICareEntityService.GetAsync"/>.
/// </summary>
internal sealed class CareEntityPhotoService : ICareEntityPhotoService
{
    private const string BannerDocumentType = "banner";
    private const string BannerSource = "personal-finance/care-entity-banner";

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IDocumentWriter _documentWriter;
    private readonly ICareEntityService _careEntityService;

    public CareEntityPhotoService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IDocumentWriter documentWriter,
        ICareEntityService careEntityService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _documentWriter = documentWriter;
        _careEntityService = careEntityService;
    }

    public async Task<CareEntityResponse?> SetPhotoAsync(
        Guid careEntityId,
        Stream image,
        string fileName,
        string contentType,
        long lengthBytes,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        var entity = await GetOwnedAsync(careEntityId, tenantId, userId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        // Validate before any blob is written — a rejected upload leaves nothing behind (Spec 049 §10).
        CareEntityBannerImage.Validate(contentType, lengthBytes);

        var ownerPartyId = await ResolveOwnerPartyIdAsync(tenantId, userId, cancellationToken);

        var document = await _documentWriter.CreateDocumentAsync(
            new CreateDocumentCommand(
                OwnerPartyId: ownerPartyId,
                DocumentType: BannerDocumentType,
                Classification: DocumentClassification.Personal,
                Source: BannerSource),
            cancellationToken);

        await _documentWriter.UploadFileAsync(
            new UploadFileCommand(document.DocumentId, fileName, contentType),
            image,
            cancellationToken);

        var previousDocumentId = entity.PhotoDocumentId;
        entity.PhotoDocumentId = document.DocumentId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Erase the replaced banner so orphaned personal images don't accumulate (Spec 049 §7.1).
        if (previousDocumentId is { } previous)
        {
            await _documentWriter.DeleteDocumentAsync(previous, cancellationToken);
        }

        // Re-read through the canonical service so the response carries the resolved signed URL.
        return await _careEntityService.GetAsync(careEntityId, cancellationToken);
    }

    public async Task<bool> RemovePhotoAsync(Guid careEntityId, CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        var entity = await GetOwnedAsync(careEntityId, tenantId, userId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        var previousDocumentId = entity.PhotoDocumentId;
        if (previousDocumentId is null)
        {
            return true; // idempotent — nothing to clear
        }

        entity.PhotoDocumentId = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _documentWriter.DeleteDocumentAsync(previousDocumentId.Value, cancellationToken);
        return true;
    }

    private async Task<CareEntity?> GetOwnedAsync(
        Guid id, Guid tenantId, Guid userId, CancellationToken cancellationToken)
        => await _dbContext.CareEntities
            .FirstOrDefaultAsync(
                e => e.Id == id && e.TenantId == tenantId && e.UserId == userId,
                cancellationToken);

    /// <summary>
    /// Resolves the current user's owning party for the created document (Spec 049 §8). The user →
    /// party mapping lives on <c>PersonalProfile</c> (the inverse of <c>PersonalFinancePartyResolver</c>).
    /// </summary>
    private async Task<Guid> ResolveOwnerPartyIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var partyId = await _dbContext.PersonalProfiles
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => (Guid?)p.PartyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (partyId is not { } resolved || resolved == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A personal profile is required before a banner image can be uploaded.");
        }

        return resolved;
    }

    private (Guid TenantId, Guid UserId) GetContext()
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return (tenantId, userId);
    }
}

/// <summary>
/// Boundary validation for a CareEntity banner upload (Spec 049 §10): an image content type from a
/// small allow-list, non-empty, and within the size cap. Throws <see cref="ArgumentException"/> on
/// failure so the endpoint maps it to 422 before any document/blob is created.
/// </summary>
internal static class CareEntityBannerImage
{
    public const long MaxBytes = 8L * 1024 * 1024; // 8 MB (Spec 049 §10 / O2)

    private static readonly HashSet<string> AllowedContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    public static void Validate(string? contentType, long lengthBytes)
    {
        if (lengthBytes <= 0)
        {
            throw new ArgumentException("The image file is empty.", nameof(lengthBytes));
        }

        if (lengthBytes > MaxBytes)
        {
            throw new ArgumentException(
                $"The image exceeds the {MaxBytes / (1024 * 1024)} MB limit.", nameof(lengthBytes));
        }

        // Strip any "; charset=" / parameters before matching.
        var normalised = (contentType ?? string.Empty).Split(';')[0].Trim();
        if (!AllowedContentTypes.Contains(normalised))
        {
            throw new ArgumentException(
                "Only JPEG, PNG, or WebP images are allowed.", nameof(contentType));
        }
    }
}
