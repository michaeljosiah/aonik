using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphValidationService
{
    private static readonly HashSet<string> AllowedNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "NativeAnnotation",
        "RelationshipAnnotation",
        "InferredAnnotation"
    };

    private static readonly HashSet<string> AllowedPredicates = new(StringComparer.OrdinalIgnoreCase)
    {
        "ANNOTATED_AS",
        "RELATED_TO_PARTY",
        "FUNDED_BY_ACCOUNT"
    };

    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FinancialLifeGraphValidationService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public async Task ValidateNodeCreateAsync(CreateFinancialLifeGraphNodeRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NodeType) || !AllowedNodeTypes.Contains(request.NodeType))
        {
            throw new ArgumentException("NodeType is invalid.", nameof(request.NodeType));
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("DisplayName is required.", nameof(request.DisplayName));
        }

        if ((request.SourceEntity is null) != (request.SourceId is null))
        {
            throw new ArgumentException("SourceEntity and SourceId must both be provided or both be null.");
        }

        if (request.IsInferred && !request.AiRunId.HasValue)
        {
            throw new ArgumentException("AiRunId is required for inferred nodes.", nameof(request.AiRunId));
        }

        await ValidateHouseholdAccessAsync(request.HouseholdId, cancellationToken);
    }

    public async Task ValidateEdgeCreateAsync(
        CreateFinancialLifeGraphEdgeRequest request,
        IReadOnlySet<string> availableNodeKeys,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Predicate) || !AllowedPredicates.Contains(request.Predicate))
        {
            throw new ArgumentException("Predicate is invalid.", nameof(request.Predicate));
        }

        if (string.IsNullOrWhiteSpace(request.FromNodeKey) || !availableNodeKeys.Contains(request.FromNodeKey))
        {
            throw new ArgumentException("FromNodeKey does not exist in the current graph.", nameof(request.FromNodeKey));
        }

        if (string.IsNullOrWhiteSpace(request.ToNodeKey) || !availableNodeKeys.Contains(request.ToNodeKey))
        {
            throw new ArgumentException("ToNodeKey does not exist in the current graph.", nameof(request.ToNodeKey));
        }

        if (request.IsInferred && !request.AiRunId.HasValue)
        {
            throw new ArgumentException("AiRunId is required for inferred edges.", nameof(request.AiRunId));
        }

        await ValidateHouseholdAccessAsync(request.HouseholdId, cancellationToken);
    }

    public async Task ValidateNodeOwnershipAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var exists = await _financeDbContext.FinancialLifeGraphNodes
            .AnyAsync(item => item.Id == nodeId && item.TenantId == tenantId && item.UserId == userId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("Financial life graph node not found.");
        }
    }

    public async Task ValidateEdgeOwnershipAsync(Guid edgeId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var exists = await _financeDbContext.FinancialLifeGraphEdges
            .AnyAsync(item => item.Id == edgeId && item.TenantId == tenantId && item.UserId == userId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException("Financial life graph edge not found.");
        }
    }

    private async Task ValidateHouseholdAccessAsync(Guid? householdId, CancellationToken cancellationToken)
    {
        if (!householdId.HasValue)
        {
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var hasAccess = await _financeDbContext.HouseholdMembers
            .AnyAsync(item => item.HouseholdId == householdId.Value && item.UserId == userId, cancellationToken);

        if (!hasAccess)
        {
            var profileAccess = await _financeDbContext.PersonalProfiles
                .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.HouseholdId == householdId.Value, cancellationToken);

            if (!profileAccess)
            {
                throw new InvalidOperationException("Household access not available for the current user.");
            }
        }
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
