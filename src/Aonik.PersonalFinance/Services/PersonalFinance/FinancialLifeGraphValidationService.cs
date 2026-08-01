using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Finance;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

internal sealed class FinancialLifeGraphValidationService
{
    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly FinancialLifeGraphSchema _schema;
    private readonly IPartyReader _partyReader;
    private readonly ICustomerOrderHistoryReader _orderReader;
    private readonly ICustomerInvoiceHistoryReader _invoiceReader;
    private readonly ICustomerPaymentHistoryReader _paymentReader;
    private readonly IGroupReader _groupReader;

    public FinancialLifeGraphValidationService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        FinancialLifeGraphSchema schema,
        IPartyReader partyReader,
        ICustomerOrderHistoryReader orderReader,
        ICustomerInvoiceHistoryReader invoiceReader,
        ICustomerPaymentHistoryReader paymentReader,
        IGroupReader groupReader)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _schema = schema;
        _partyReader = partyReader;
        _orderReader = orderReader;
        _invoiceReader = invoiceReader;
        _paymentReader = paymentReader;
        _groupReader = groupReader;
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

        var existingDisplayNames = await _dbContext.FinancialLifeGraphNodes
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
            var existingSourceEntities = await _dbContext.FinancialLifeGraphNodes
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

        await ValidateCanonicalRelationshipConflictsAsync(request, fromNodeType, toNodeType, cancellationToken);

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
        var duplicateEdgeExists = await _dbContext.FinancialLifeGraphEdges
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

        var profile = await _dbContext.PersonalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken);

        foreach (var nodeKey in normalizedKeys)
        {
            if (!FinancialLifeGraphNodeKey.TryParse(nodeKey, out var parsedNodeKey))
            {
                continue;
            }

            var nodeType = await ResolveNodeTypeAsync(parsedNodeKey.Prefix, parsedNodeKey.Id, tenantId, userId, profile, cancellationToken);
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
        var exists = await _dbContext.FinancialLifeGraphNodes
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
        var exists = await _dbContext.FinancialLifeGraphEdges
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

        // Spec 086 P7 — asked of the group reader rather than the table. The question has never been
        // a personal-finance one: it is "is this user in this group", and the reader already returns
        // accepted members only.
        // The group kind is checked as well as membership. HouseholdId is request-controlled, so a
        // member of a FAMILY could otherwise pass its id here and have the graph endpoints persist
        // nodes and edges scoped to another product's group.
        var group = await _groupReader.GetAsync(householdId.Value, cancellationToken);
        var isHousehold = group is not null
            && (string.IsNullOrEmpty(group.Kind)
                || string.Equals(group.Kind, GroupKinds.Household, StringComparison.OrdinalIgnoreCase));

        var members = isHousehold
            ? await _groupReader.GetMembersAsync(householdId.Value, cancellationToken)
            : [];

        var hasAccess = members.Any(member => member.UserId == userId);

        if (!hasAccess)
        {
            var profileAccess = await _dbContext.PersonalProfiles
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

    private async Task ValidateCanonicalRelationshipConflictsAsync(
        CreateFinancialLifeGraphEdgeRequest request,
        string fromNodeType,
        string toNodeType,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Predicate.Trim(), FinancialLifeGraphPredicates.RelatedToParty, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(fromNodeType, FinancialLifeGraphNodeTypes.UserRoot, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(toNodeType, FinancialLifeGraphNodeTypes.Party, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var profile = await _dbContext.PersonalProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId, cancellationToken);

        if (profile?.PartyId is not Guid selfPartyId)
        {
            return;
        }

        if (!FinancialLifeGraphNodeKey.TryParse(request.ToNodeKey.Trim(), out var partyNodeKey)
            || !string.Equals(partyNodeKey.Prefix, FinancialLifeGraphNodeKeys.Party, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var canonicalExists = await _partyReader.HasActiveRelationshipBetweenAsync(
            tenantId, selfPartyId, partyNodeKey.Id, cancellationToken);

        if (canonicalExists)
        {
            throw new InvalidOperationException("A canonical PartyRelationship already represents this related party link; native RELATED_TO_PARTY edges are not allowed for it.");
        }
    }

    private async Task<string?> ResolveNodeTypeAsync(
        string prefix,
        Guid nodeId,
        Guid tenantId,
        Guid userId,
        Aonik.PersonalFinance.Entities.PersonalProfile? profile,
        CancellationToken cancellationToken)
    {
        switch (prefix)
        {
            case FinancialLifeGraphNodeKeys.User:
                return nodeId == userId ? FinancialLifeGraphNodeTypes.UserRoot : null;

            case FinancialLifeGraphNodeKeys.Household:
                if (profile?.HouseholdId != nodeId)
                {
                    return null;
                }

                return await _groupReader.ExistsAsync(nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.Household
                    : null;

            case FinancialLifeGraphNodeKeys.HouseholdMember:
                if (profile?.HouseholdId is not Guid currentHouseholdId)
                {
                    return null;
                }

                var householdMembers = (await _groupReader.GetMembersAsync(currentHouseholdId, cancellationToken))
                    .Where(item => item.Id == nodeId)
                    .ToList();

                // The reader returns accepted members only, so existence in that list IS acceptance.
                return householdMembers.Count > 0
                    ? FinancialLifeGraphNodeTypes.HouseholdMember
                    : null;

            case FinancialLifeGraphNodeKeys.Party:
                if (profile?.PartyId is not Guid currentPartyId)
                {
                    return null;
                }

                var isRelatedParty = await _partyReader.HasActiveRelationshipBetweenAsync(
                    tenantId, currentPartyId, nodeId, cancellationToken);

                if (!isRelatedParty)
                {
                    return null;
                }

                return await _partyReader.ExistsAsync(tenantId, nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.Party
                    : null;

            case FinancialLifeGraphNodeKeys.PersonalAccount:
                if (await _dbContext.PersonalAccounts
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    )
                {
                    return FinancialLifeGraphNodeTypes.PersonalAccount;
                }

                if (profile?.HouseholdId is not Guid accountHouseholdId)
                {
                    return null;
                }

                var householdMemberships = (await _groupReader.GetMembersAsync(accountHouseholdId, cancellationToken))
                    .Where(item => item.UserId == userId)
                    .ToList();

                if (householdMemberships.Count == 0)
                {
                    return null;
                }

                return await _dbContext.PersonalAccounts
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.Id == nodeId && item.HouseholdId == accountHouseholdId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.PersonalAccount
                    : null;

            case FinancialLifeGraphNodeKeys.LinkedAccount:
                return await _dbContext.PersonalLinkedAccounts
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.PersonalLinkedAccount
                    : null;

            case FinancialLifeGraphNodeKeys.PersonalTransaction:
                return await _dbContext.PersonalTransactions
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.PersonalTransaction
                    : null;

            case FinancialLifeGraphNodeKeys.Bill:
                return await _dbContext.Bills
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.Bill
                    : null;

            case FinancialLifeGraphNodeKeys.Goal:
                return await _dbContext.Goals
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.Goal
                    : null;

            case FinancialLifeGraphNodeKeys.Subscription:
                return await _dbContext.Subscriptions
                    .AsNoTracking()
                    .AnyAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.Subscription
                    : null;

            case FinancialLifeGraphNodeKeys.NativeNode:
                var nativeNode = await _dbContext.FinancialLifeGraphNodes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == userId && item.Id == nodeId && item.Status == FinancialLifeGraphEntityStatus.Active, cancellationToken);
                return nativeNode?.NodeType;

            case FinancialLifeGraphNodeKeys.OrderRef:
                return await _orderReader.ExistsAsync(tenantId, nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.OrderRef
                    : null;

            case FinancialLifeGraphNodeKeys.InvoiceRef:
                return await _invoiceReader.ExistsAsync(tenantId, nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.InvoiceRef
                    : null;

            case FinancialLifeGraphNodeKeys.PaymentIntentRef:
                return await _paymentReader.ExistsAsync(tenantId, nodeId, cancellationToken)
                    ? FinancialLifeGraphNodeTypes.PaymentIntentRef
                    : null;

            default:
                return null;
        }
    }
}
