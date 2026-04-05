using Aonik.Platform.Contracts.Models.Compliance;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Compliance;

internal sealed class AuditLogAdminService : IAuditLogAdminService
{
    private readonly PlatformDbContext _dbContext;

    public AuditLogAdminService(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<AuditLogListItem>> ListAuditLogsAsync(
        ListAuditLogsRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var search = request.Search?.Trim();
        var action = request.Action?.Trim();
        var resourceType = request.ResourceType?.Trim();
        var correlationId = request.CorrelationId?.Trim();

        var query = _dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(resourceType))
        {
            query = query.Where(x => x.ResourceType == resourceType);
        }

        if (request.ResourceId.HasValue)
        {
            query = query.Where(x => x.ResourceId == request.ResourceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            query = query.Where(x => x.CorrelationId == correlationId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Action.Contains(search)
                || x.ResourceType.Contains(search)
                || x.CorrelationId.Contains(search)
                || x.DetailsJson.Contains(search)
                || x.ActorType.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AuditLogListItem(
                x.Id,
                x.TenantId,
                x.Timestamp,
                x.ActorType,
                x.ActorId,
                x.Action,
                x.ResourceType,
                x.ResourceId,
                x.DetailsJson,
                x.CorrelationId))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogListItem>(items, totalCount, pageNumber, pageSize);
    }
}
