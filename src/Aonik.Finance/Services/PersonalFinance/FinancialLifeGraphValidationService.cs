using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialLifeGraphValidationService
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly FinancialLifeGraphSchema _schema;

    public FinancialLifeGraphValidationService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        FinancialLifeGraphSchema schema)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _schema = schema;
    }

    public async Task ValidateNodeCreateAsync(CreateFinancialLifeGraphNodeRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.NodeType))
        {
            throw new ArgumentException("NodeType is invalid.", nameof(request.NodeType));
        }

        var nodeType = request.NodeType.Trim();
        if (!_schema.TryGetNodeType(nodeType, out var definition) || definition is null)
        {
            throw new ArgumentException($"NodeType '{nodeType}' is not defined in the Financial Life Graph schema.", nameof(request.NodeType));
        }

        if (!definition.CanBeCreatedNatively)
        {
            throw new InvalidOperationException($"NodeType '{nodeType}' is reserved for mirror projection and cannot be created through native graph writes.");
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

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var normalizedDisplayName = request.DisplayName.Trim();

        var existingDisplayNames = await _financeDbContext.FinancialLifeGraphNodes
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.UserId == userId
                && item.NodeType == nodeType
                && item.Status != FinancialLifeGraphEntityStatus.Rejected)
            .Select(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        var duplicateDisplayNameExists = existingDisplayNames
            .Any(item => string.Equals(item?.Trim(), normalizedDisplayName, StringComparison.OrdinalIgnoreCase));

        if (duplicateDisplayNameExists)
        {
            throw new InvalidOperationException("A graph node with the same type and display name already exists.");
        }

        if (!string.IsNullOrWhiteSpace(request.SourceEntity) && request.SourceId.HasValue)
        {
            var normalizedSourceEntity = request.SourceEntity.Trim();
            var existingSourceEntities = await _financeDbContext.FinancialLifeGraphNodes
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && item.UserId == userId
                    && item.SourceId == request.SourceId.Value
                    && item.Status != FinancialLifeGraphEntityStatus.Rejected)
                .Select(item => item.SourceEntity)
                .ToListAsync(cancellationToken);

            var duplicateSourceExists = existingSourceEntities
                .Any(item => !string.IsNullOrWhiteSpace(item)
                    && string.Equals(item.Trim(), normalizedSourceEntity, StringComparison.OrdinalIgnoreCase));

            if (duplicateSourceExists)
            {
                throw new InvalidOperationException("A graph node for the same source entity already exists.");
            }
        }

        await ValidateHouseholdAccessAsync(request.HouseholdId, cancellationToken);
    }

    public async Task ValidateEdgeCreateAsync(
        CreateFinancialLifeGraphEdgeRequest request,
        IReadOnlyDictionary<string, string> availableNodeTypesByKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Predicate))
        {
            throw new ArgumentException("Predicate is invalid.", nameof(request.Predicate));
        }

        var predicate = request.Predicate.Trim();
        if (!_schema.IsKnownPredicate(predicate))
        {
            throw new ArgumentException($"Predicate '{predicate}' is not defined in the Financial Life Graph schema.", nameof(request.Predicate));
        }

        if (string.IsNullOrWhiteSpace(request.FromNodeKey) || !availableNodeTypesByKey.TryGetValue(request.FromNodeKey.Trim(), out var fromNodeType))
        {
            throw new ArgumentException("FromNodeKey does not exist in the current graph.", nameof(request.FromNodeKey));
        }

        if (string.IsNullOrWhiteSpace(request.ToNodeKey) || !availableNodeTypesByKey.TryGetValue(request.ToNodeKey.Trim(), out var toNodeType))
        {
            throw new ArgumentException("ToNodeKey does not exist in the current graph.", nameof(request.ToNodeKey));
        }

        if (request.IsInferred && !request.AiRunId.HasValue)
        {
            throw new ArgumentException("AiRunId is required for inferred edges.", nameof(request.AiRunId));
        }

        if (!_schema.IsAllowedEdge(fromNodeType, predicate, toNodeType, requireNativeCreatable: true))
        {
            throw new InvalidOperationException(
                $"Edge combination '{fromNodeType} -> {predicate} -> {toNodeType}' is not permitted by the Financial Life Graph schema.");
        }

        await EnsureEdgeDoesNotAlreadyExistAsync(request, cancellationToken);

        await ValidateHouseholdAccessAsync(request.HouseholdId, cancellationToken);
    }

    public async Task EnsureEdgeDoesNotAlreadyExistAsync(
        CreateFinancialLifeGraphEdgeRequest request,
        CancellationToken cancellationToken = default)
    {
        var predicate = request.Predicate.Trim();

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var duplicateEdgeExists = await _financeDbContext.FinancialLifeGraphEdges
            .AnyAsync(item => item.TenantId == tenantId
                && item.UserId == userId
                && item.FromNodeKey == request.FromNodeKey.Trim()
                && item.Predicate == predicate
                && item.ToNodeKey == request.ToNodeKey.Trim()
                && item.Status != FinancialLifeGraphEntityStatus.Rejected, cancellationToken);

        if (duplicateEdgeExists)
        {
            throw new InvalidOperationException("A graph edge with the same shape already exists.");
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveAccessibleNodeTypesAsync(
        IEnumerable<string> nodeKeys,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var normalizedKeys = nodeKeys
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (normalizedKeys.Count == 0)
        {
            return result;
        }

        var profile = await _financeDbContext.PersonalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken);

        foreach (var nodeKey in normalizedKeys)
        {
            if (!TryParseNodeKey(nodeKey, out var prefix, out var nodeId))
            {
                continue;
            }

            var nodeType = await ResolveNodeTypeAsync(prefix, nodeId, tenantId, userId, profile, cancellationToken);
            if (!string.IsNullOrWhiteSpace(nodeType))
            {
                result[nodeKey] = nodeType;
            }
        }

        return result;
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
            .AnyAsync(item => item.TenantId == tenantId && item.HouseholdId == householdId.Value && item.UserId == userId, cancellationToken);

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

    private async Task<string?> ResolveNodeTypeAsync(
        string prefix,
        Guid nodeId,
        Guid tenantId,
        Guid userId,
        Aonik.Finance.Entities.PersonalFinance.PersonalProfile? profile,
        CancellationToken cancellationToken)
    {
        switch (prefix)
        {
            case "user":
                return nodeId == userId ? FinancialLifeGraphNodeTypes.UserRoot : null;

            case "household":
                if (profile?.HouseholdId != nodeId)
                {
                    return null;
                }

                return await _financeDbContext.Households
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.Household
                    : null;

            case "household-member":
                if (profile?.HouseholdId is not Guid currentHouseholdId)
                {
                    return null;
                }

                return await _financeDbContext.HouseholdMembers
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.Id == nodeId && item.HouseholdId == currentHouseholdId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.HouseholdMember
                    : null;

            case "party":
                if (profile?.PartyId is not Guid currentPartyId)
                {
                    return null;
                }

                var isRelatedParty = await _financeDbContext.PartyRelationships
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId
                        && item.IsActive
                        && ((item.FromPartyId == currentPartyId && item.ToPartyId == nodeId)
                            || (item.ToPartyId == currentPartyId && item.FromPartyId == nodeId)), cancellationToken);

                if (!isRelatedParty)
                {
                    return null;
                }

                return await _financeDbContext.Parties
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.Party
                    : null;

            case "personal-account":
                return await _financeDbContext.PersonalAccounts
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.PersonalAccount
                    : null;

            case "linked-account":
                return await _financeDbContext.FinancialLinkedAccounts
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.FinancialLinkedAccount
                    : null;

            case "personal-transaction":
                return await _financeDbContext.PersonalTransactions
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.PersonalTransaction
                    : null;

            case "bill":
                return await _financeDbContext.Bills
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.Bill
                    : null;

            case "goal":
                return await _financeDbContext.Goals
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.Goal
                    : null;

            case "subscription":
                return await _financeDbContext.Subscriptions
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.Subscription
                    : null;

            case "native-node":
                var nativeNode = await _financeDbContext.FinancialLifeGraphNodes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId && item.Status == FinancialLifeGraphEntityStatus.Active, cancellationToken);
                return nativeNode?.NodeType;

            default:
                return null;
        }
    }

    private static bool TryParseNodeKey(string nodeKey, out string prefix, out Guid nodeId)
    {
        prefix = string.Empty;
        nodeId = Guid.Empty;

        var parts = nodeKey.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out nodeId))
        {
            return false;
        }

        prefix = parts[0].Trim().ToLowerInvariant();
        return true;
    }
}
