using System.Text.Json;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.PersonalFinance.Services;

internal sealed class CareEntityService : ICareEntityService
{
    private static readonly IReadOnlyDictionary<string, string> EmptyAttributes =
        new Dictionary<string, string>();

    /// <summary>TTL of the signed banner URL embedded in entity responses (Spec 049 §9).</summary>
    private static readonly TimeSpan PhotoUrlTtl = TimeSpan.FromMinutes(15);

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IDocumentReader _documentReader;
    private readonly ILogger<CareEntityService> _logger;

    public CareEntityService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IDocumentReader documentReader,
        ILogger<CareEntityService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _documentReader = documentReader;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CareEntityResponse>> ListAsync(
        string? kind = null,
        string? assetType = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        var query = _dbContext.CareEntities
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.UserId == userId);

        if (!includeArchived)
        {
            query = query.Where(e => !e.Archived);
        }

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var normalisedKind = kind.Trim().ToLowerInvariant();
            query = query.Where(e => e.Kind == normalisedKind);
        }

        if (!string.IsNullOrWhiteSpace(assetType))
        {
            var normalisedAssetType = assetType.Trim().ToLowerInvariant();
            query = query.Where(e => e.AssetType == normalisedAssetType);
        }

        var entities = await query
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

        // List omits PhotoUrl (resolved only by Get/Create/Update + profile) to avoid an
        // N+1 over the Documents module across the whole grid (Spec 049 §9).
        return entities.Select(e => MapToResponse(e)).ToList();
    }

    public async Task<CareEntityResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        return entity is null ? null : await MapWithPhotoAsync(entity, cancellationToken);
    }

    public async Task<CareEntityResponse> CreateAsync(
        CreateCareEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = GetContext();

        var kind = (request.Kind ?? string.Empty).Trim().ToLowerInvariant();
        if (kind is not ("person" or "asset" or "organization"))
        {
            throw new ArgumentException("Kind must be 'person', 'asset', or 'organization'.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name is required.", nameof(request));
        }

        var entity = new CareEntity
        {
            TenantId = tenantId,
            UserId = userId,
            Kind = kind,
            AssetType = NormalizeAssetType(kind, request.AssetType),
            Name = request.Name.Trim(),
            CountryCode = NormalizeCountry(request.CountryCode),
            Relationship = Clean(request.Relationship),
            Emoji = Clean(request.Emoji),
            PhotoDocumentId = request.PhotoDocumentId,
            AttributesJson = SerializeAttributes(request.Attributes),
            Archived = false,
        };

        _dbContext.CareEntities.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await MapWithPhotoAsync(entity, cancellationToken);
    }

    public async Task<CareEntityResponse?> UpdateAsync(
        Guid id,
        UpdateCareEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name is required.", nameof(request));
        }

        // Kind is immutable — a person never becomes an asset. AssetType must
        // therefore stay consistent with the entity's existing kind.
        entity.AssetType = NormalizeAssetType(entity.Kind, request.AssetType);
        entity.Name = request.Name.Trim();
        entity.CountryCode = NormalizeCountry(request.CountryCode);
        entity.Relationship = Clean(request.Relationship);
        entity.Emoji = Clean(request.Emoji);
        entity.PhotoDocumentId = request.PhotoDocumentId;
        entity.AttributesJson = SerializeAttributes(request.Attributes);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await MapWithPhotoAsync(entity, cancellationToken);
    }

    public async Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAsync(id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.Archived = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<CareEntity?> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = GetContext();
        return await _dbContext.CareEntities
            .FirstOrDefaultAsync(
                e => e.Id == id && e.TenantId == tenantId && e.UserId == userId,
                cancellationToken);
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

    /// <summary>
    /// Enforces the kind ↔ assetType invariant (Spec 043 §6, generalised by Spec 049 §5):
    /// only a <c>kind = asset</c> may carry an assetType — <c>person</c> and
    /// <c>organization</c> must not — and an asset must carry one. Validators enforce this
    /// at the boundary for create; this is the authoritative check for update (where the
    /// route DTO does not know the entity's kind).
    /// </summary>
    private static string? NormalizeAssetType(string kind, string? assetType)
    {
        if (kind != "asset")
        {
            if (!string.IsNullOrWhiteSpace(assetType))
            {
                throw new ArgumentException("Only an asset can have an assetType.", nameof(assetType));
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(assetType))
        {
            throw new ArgumentException("An asset must have an assetType.", nameof(assetType));
        }

        return assetType.Trim().ToLowerInvariant();
    }

    private static string NormalizeCountry(string? countryCode)
        => (countryCode ?? string.Empty).Trim().ToUpperInvariant();

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SerializeAttributes(IReadOnlyDictionary<string, string>? attributes)
        => attributes is null || attributes.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(attributes);

    private static IReadOnlyDictionary<string, string> DeserializeAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return EmptyAttributes;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return EmptyAttributes;
        }
    }

    /// <summary>Maps the entity and resolves a signed banner URL from <c>PhotoDocumentId</c> (Spec 049 §9).</summary>
    private async Task<CareEntityResponse> MapWithPhotoAsync(CareEntity e, CancellationToken cancellationToken)
        => MapToResponse(e, await ResolvePhotoUrlAsync(e.PhotoDocumentId, cancellationToken));

    /// <summary>
    /// Resolves a document pointer to a time-limited signed read URL via the Documents module.
    /// Returns null (never throws) when there is no photo or the document/file is gone — a broken
    /// banner must never fail the entity read.
    /// </summary>
    private async Task<Uri?> ResolvePhotoUrlAsync(Guid? photoDocumentId, CancellationToken cancellationToken)
    {
        if (photoDocumentId is not { } documentId)
        {
            return null;
        }

        try
        {
            var files = await _documentReader.GetFilesAsync(documentId, cancellationToken);
            var file = files.FirstOrDefault();
            if (file is null)
            {
                return null;
            }

            return await _documentReader.GetReadUrlAsync(file.DocumentFileId, PhotoUrlTtl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve banner URL for document {DocumentId}", documentId);
            return null;
        }
    }

    private static CareEntityResponse MapToResponse(CareEntity e, Uri? photoUrl = null)
        => new(
            e.Id,
            e.Kind,
            e.AssetType,
            e.Name,
            e.CountryCode,
            e.Relationship,
            e.Emoji,
            e.PhotoDocumentId,
            photoUrl,
            DeserializeAttributes(e.AttributesJson),
            e.Archived,
            e.CreatedAt,
            e.UpdatedAt);
}
