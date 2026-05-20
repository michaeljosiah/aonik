using System.Data;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphWriteService
{
    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly FinancialLifeGraphValidationService _validationService;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public FinancialLifeGraphWriteService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        FinancialLifeGraphValidationService validationService,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _validationService = validationService;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<FinancialLifeGraphNodeWriteResponse> CreateNodeAsync(
        CreateFinancialLifeGraphNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validationService.ValidateNodeCreateAsync(request, cancellationToken);

        var node = new FinancialLifeGraphNode
        {
            TenantId = _tenantProvider.GetCurrentTenantId(),
            UserId = GetCurrentUserId(),
            HouseholdId = request.HouseholdId,
            NodeType = request.NodeType.Trim(),
            DisplayName = request.DisplayName.Trim(),
            SourceEntity = string.IsNullOrWhiteSpace(request.SourceEntity) ? null : request.SourceEntity.Trim(),
            SourceId = request.SourceId,
            PropertiesJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? "{}" : request.MetadataJson,
            Status = request.Status ?? FinancialLifeGraphEntityStatus.Active,
            IsInferred = request.IsInferred,
            AiRunId = request.AiRunId
        };

        _dbContext.FinancialLifeGraphNodes.Add(node);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

        return new FinancialLifeGraphNodeWriteResponse(node.Id, $"native-node:{node.Id:D}");
    }

    public async Task<FinancialLifeGraphEdgeWriteResponse> CreateEdgeAsync(
        CreateFinancialLifeGraphEdgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var useTransaction = _dbContext.Database.IsRelational();
        var committed = false;
        await using var transaction = useTransaction
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        try
        {
            var requestedNodeKeys = new[] { request.FromNodeKey, request.ToNodeKey };
            var availableNodeTypesByKey = await _validationService.ResolveAccessibleNodeTypesAsync(requestedNodeKeys, cancellationToken);

            await _validationService.ValidateEdgeCreateAsync(request, availableNodeTypesByKey, cancellationToken);

            var edge = new FinancialLifeGraphEdge
            {
                TenantId = _tenantProvider.GetCurrentTenantId(),
                UserId = GetCurrentUserId(),
                HouseholdId = request.HouseholdId,
                FromNodeKey = request.FromNodeKey.Trim(),
                Predicate = request.Predicate.Trim(),
                ToNodeKey = request.ToNodeKey.Trim(),
                PropertiesJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? "{}" : request.MetadataJson,
                Status = request.Status ?? FinancialLifeGraphEntityStatus.Active,
                IsInferred = request.IsInferred,
                AiRunId = request.AiRunId
            };

            _dbContext.FinancialLifeGraphEdges.Add(edge);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var revalidatedNodeTypes = await _validationService.ResolveAccessibleNodeTypesAsync(requestedNodeKeys, cancellationToken);
            if (!revalidatedNodeTypes.ContainsKey(request.FromNodeKey.Trim())
                || !revalidatedNodeTypes.ContainsKey(request.ToNodeKey.Trim()))
            {
                if (!useTransaction)
                {
                    _dbContext.FinancialLifeGraphEdges.Remove(edge);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw new InvalidOperationException("One or more graph edge targets no longer exist in the current graph scope.");
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
                committed = true;
            }

            await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

            return new FinancialLifeGraphEdgeWriteResponse(edge.Id);
        }
        catch
        {
            if (transaction != null && !committed)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }

    public async Task DeleteNodeAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        await _validationService.ValidateNodeOwnershipAsync(nodeId, cancellationToken);

        var node = await _dbContext.FinancialLifeGraphNodes
            .FirstOrDefaultAsync(item => item.Id == nodeId, cancellationToken)
            ?? throw new InvalidOperationException("Financial life graph node not found.");

        var edges = await _dbContext.FinancialLifeGraphEdges
            .Where(item => item.FromNodeKey == $"native-node:{node.Id:D}" || item.ToNodeKey == $"native-node:{node.Id:D}")
            .ToListAsync(cancellationToken);

        if (edges.Count > 0)
        {
            _dbContext.FinancialLifeGraphEdges.RemoveRange(edges);
        }

        _dbContext.FinancialLifeGraphNodes.Remove(node);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
    }

    public async Task DeleteEdgeAsync(Guid edgeId, CancellationToken cancellationToken = default)
    {
        await _validationService.ValidateEdgeOwnershipAsync(edgeId, cancellationToken);

        var edge = await _dbContext.FinancialLifeGraphEdges
            .FirstOrDefaultAsync(item => item.Id == edgeId, cancellationToken)
            ?? throw new InvalidOperationException("Financial life graph edge not found.");

        _dbContext.FinancialLifeGraphEdges.Remove(edge);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }
}
