using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Cms;
using Aonik.Domain.Cms.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Services.Cms;

public class ContentBlockService : IContentBlockService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public ContentBlockService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<ContentBlockResponse> CreateContentBlockAsync(
        CreateContentBlockRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var contentBlock = new ContentBlock
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ContentKey = request.ContentKey,
            Title = request.Title,
            Slug = request.Slug,
            Area = Enum.Parse<ContentBlockArea>(request.Area),
            Format = Enum.Parse<ContentBlockFormat>(request.Format),
            Body = request.Body,
            Locale = request.Locale,
            IsEnabled = request.IsEnabled,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Priority = request.Priority,
            TargetingJson = request.TargetingJson,
            Media = new List<ContentBlockMedia>()
        };

        _dbContext.ContentBlocks.Add(contentBlock);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(contentBlock);
    }

    public async Task<ContentBlockResponse?> GetContentBlockAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var contentBlock = await _dbContext.ContentBlocks
            .AsNoTracking()
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return contentBlock == null ? null : MapToResponse(contentBlock);
    }

    public async Task<ContentBlockResponse?> GetContentBlockByKeyAsync(
        string contentKey,
        string locale,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var contentBlock = await _dbContext.ContentBlocks
            .AsNoTracking()
            .Include(x => x.Media)
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && 
                     x.ContentKey == contentKey && 
                     x.Locale == locale,
                cancellationToken);

        return contentBlock == null ? null : MapToResponse(contentBlock);
    }

    public async Task<List<ContentBlockResponse>> ListContentBlocksAsync(
        ContentBlockListRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _dbContext.ContentBlocks
            .AsNoTracking()
            .Include(x => x.Media)
            .Where(x => x.TenantId == tenantId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(request.Area))
        {
            query = query.Where(x => x.Area == Enum.Parse<ContentBlockArea>(request.Area));
        }

        if (!string.IsNullOrEmpty(request.ContentKey))
        {
            query = query.Where(x => x.ContentKey.Contains(request.ContentKey));
        }

        if (!string.IsNullOrEmpty(request.Locale))
        {
            query = query.Where(x => x.Locale == request.Locale);
        }

        if (request.IsEnabled.HasValue)
        {
            query = query.Where(x => x.IsEnabled == request.IsEnabled.Value);
        }

        var contentBlocks = await query
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return contentBlocks.Select(MapToResponse).ToList();
    }

    public async Task<ContentBlockResponse> UpdateContentBlockAsync(
        Guid id,
        UpdateContentBlockRequest request,
        CancellationToken cancellationToken = default)
    {
        var contentBlock = await _dbContext.ContentBlocks
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (contentBlock == null)
            throw new InvalidOperationException($"Content block {id} not found");

        contentBlock.Title = request.Title;
        contentBlock.Slug = request.Slug;
        contentBlock.Area = Enum.Parse<ContentBlockArea>(request.Area);
        contentBlock.Format = Enum.Parse<ContentBlockFormat>(request.Format);
        contentBlock.Body = request.Body;
        contentBlock.Locale = request.Locale;
        contentBlock.IsEnabled = request.IsEnabled;
        contentBlock.StartAt = request.StartAt;
        contentBlock.EndAt = request.EndAt;
        contentBlock.Priority = request.Priority;
        contentBlock.TargetingJson = request.TargetingJson;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(contentBlock);
    }

    public async Task DeleteContentBlockAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var contentBlock = await _dbContext.ContentBlocks
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (contentBlock == null)
            throw new InvalidOperationException($"Content block {id} not found");

        _dbContext.ContentBlocks.Remove(contentBlock);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContentBlockMediaResponse> AddMediaAsync(
        Guid contentBlockId,
        AddContentBlockMediaRequest request,
        CancellationToken cancellationToken = default)
    {
        var contentBlock = await _dbContext.ContentBlocks
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Id == contentBlockId, cancellationToken);

        if (contentBlock == null)
            throw new InvalidOperationException($"Content block {contentBlockId} not found");

        var maxOrder = contentBlock.Media.Any() ? contentBlock.Media.Max(x => x.Order) : -1;

        var media = new ContentBlockMedia
        {
            Id = Guid.NewGuid(),
            ContentBlockId = contentBlockId,
            StorageType = "Url",
            Url = request.Url,
            Alt = request.Alt,
            Caption = request.Caption,
            MimeType = request.MimeType,
            Order = maxOrder + 1,
            LinkUrl = request.LinkUrl
        };

        contentBlock.Media.Add(media);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToMediaResponse(media);
    }

    public async Task RemoveMediaAsync(
        Guid contentBlockId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var contentBlock = await _dbContext.ContentBlocks
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Id == contentBlockId, cancellationToken);

        if (contentBlock == null)
            throw new InvalidOperationException($"Content block {contentBlockId} not found");

        var media = contentBlock.Media.FirstOrDefault(x => x.Id == mediaId);
        if (media == null)
            throw new InvalidOperationException($"Media {mediaId} not found");

        contentBlock.Media.Remove(media);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderMediaAsync(
        Guid contentBlockId,
        List<Guid> mediaIdsInOrder,
        CancellationToken cancellationToken = default)
    {
        var contentBlock = await _dbContext.ContentBlocks
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Id == contentBlockId, cancellationToken);

        if (contentBlock == null)
            throw new InvalidOperationException($"Content block {contentBlockId} not found");

        for (int i = 0; i < mediaIdsInOrder.Count; i++)
        {
            var media = contentBlock.Media.FirstOrDefault(x => x.Id == mediaIdsInOrder[i]);
            if (media != null)
            {
                media.Order = i;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ContentBlockResponse>> GetActiveContentBlocksAsync(
        string area,
        string locale,
        CancellationToken cancellationToken = default)
    {
        // For anonymous/public endpoints, tenant context may not be available
        if (!_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            return new List<ContentBlockResponse>();
        }

        var now = DateTimeOffset.UtcNow;

        var contentBlocks = await _dbContext.ContentBlocks
            .AsNoTracking()
            .Include(x => x.Media)
            .Where(x => x.TenantId == tenantId &&
                        x.Area == Enum.Parse<ContentBlockArea>(area) &&
                        x.Locale == locale &&
                        x.IsEnabled &&
                        (x.StartAt == null || x.StartAt <= now) &&
                        (x.EndAt == null || x.EndAt >= now))
            .OrderBy(x => x.Priority)
            .ToListAsync(cancellationToken);

        return contentBlocks.Select(MapToResponse).ToList();
    }

    private static ContentBlockResponse MapToResponse(ContentBlock contentBlock)
    {
        return new ContentBlockResponse(
            contentBlock.Id,
            contentBlock.ContentKey,
            contentBlock.Title,
            contentBlock.Slug,
            contentBlock.Area.ToString(),
            contentBlock.Format.ToString(),
            contentBlock.Body,
            contentBlock.Locale,
            contentBlock.IsEnabled,
            contentBlock.StartAt,
            contentBlock.EndAt,
            contentBlock.Priority,
            contentBlock.Media.OrderBy(m => m.Order).Select(MapToMediaResponse).ToList(),
            contentBlock.CreatedAt,
            contentBlock.UpdatedAt);
    }

    private static ContentBlockMediaResponse MapToMediaResponse(ContentBlockMedia media)
    {
        return new ContentBlockMediaResponse(
            media.Id,
            media.StorageType,
            media.Url,
            media.Alt,
            media.Caption,
            media.MimeType,
            media.Order,
            media.LinkUrl);
    }
}
