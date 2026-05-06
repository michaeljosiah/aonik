using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Persistence;
using Aonik.Platform.Persistence;
using Aonik.Finance.Persistence;

namespace Aonik.Platform.Services.Seeding.Phases;

/// <summary>
/// Removes all demo-seeded data in the correct dependency order:
/// activity → notifications → orders → households → workflows →
/// catalog+pricing → partner network → parties → tenant coverage.
/// Called by DemoSeedService.ReverseAsync.
/// </summary>
internal sealed class ReverseSeedPhase
{
    private static readonly PlatformDemoSeedNames SeedNames = PlatformDemoSeedNames.Instance;

    private static readonly string[] DemoWorkflowSlugs = SeedNames.WorkflowSlugs;
    private static readonly string[] DemoNotificationTypes = SeedNames.NotificationTypes;
    private static readonly string[] DemoAgentNames = SeedNames.AgentNames;
    private static readonly string[] DemoPartnerNames = SeedNames.PartnerNames;

    private readonly PlatformDbContext _dbContext;
    private readonly FinanceDbContext _financeDbContext;
    private readonly IAgentDemoCleanup _agentDemoCleanup;

    public ReverseSeedPhase(
        PlatformDbContext dbContext,
        FinanceDbContext financeDbContext,
        IAgentDemoCleanup agentDemoCleanup)
    {
        _dbContext = dbContext;
        _financeDbContext = financeDbContext;
        _agentDemoCleanup = agentDemoCleanup;
    }

    public async Task ReverseAgentActivityAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var counts = await _agentDemoCleanup.RemoveAgentActivityAsync(
            tenantId, DemoAgentNames, cancellationToken);

        if (counts.ProposalsDeleted > 0 || counts.AgentRunsDeleted > 0)
        {
            operations.Add($"Removed {counts.ProposalsDeleted} proposals and {counts.AgentRunsDeleted} agent runs");
        }
    }

    public async Task ReverseNotificationsAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var count = await _dbContext.Notifications
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId
                           && DemoNotificationTypes.Contains(item.Type))
            .ExecuteDeleteAsync(cancellationToken);

        if (count > 0)
        {
            operations.Add($"Removed {count} demo notifications");
        }
    }

    public async Task ReverseOrdersAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var orderIds = await _financeDbContext.Orders
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && item.ProvenanceJson.Contains("demo-seed"))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (orderIds.Count == 0)
        {
            return;
        }

        await _financeDbContext.OrderPartyRoles
            .IncludeSoftDeleted()
            .Where(item => orderIds.Contains(item.OrderId))
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.OrderItems
            .IncludeSoftDeleted()
            .Where(item => orderIds.Contains(item.OrderId))
            .ExecuteDeleteAsync(cancellationToken);

        var orderCount = await _financeDbContext.Orders
            .IncludeSoftDeleted()
            .Where(item => orderIds.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);

        operations.Add($"Removed {orderCount} demo orders");
    }

    public async Task ReverseHouseholdsAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var householdNames = SeedNames.HouseholdNames;
        var householdIds = await _financeDbContext.Households
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && householdNames.Contains(item.Name))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (householdIds.Count == 0)
        {
            return;
        }

        await _financeDbContext.HouseholdMembers
            .IncludeSoftDeleted()
            .Where(item => householdIds.Contains(item.HouseholdId))
            .ExecuteDeleteAsync(cancellationToken);

        var householdCount = await _financeDbContext.Households
            .IncludeSoftDeleted()
            .Where(item => householdIds.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);

        operations.Add($"Removed {householdCount} demo households");
    }

    public async Task ReverseWorkflowRegistryAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var counts = await _agentDemoCleanup.RemoveWorkflowsAndAgentsAsync(
            tenantId, DemoWorkflowSlugs, DemoAgentNames, cancellationToken);

        if (counts.WorkflowsDeleted > 0)
        {
            operations.Add($"Removed {counts.WorkflowsDeleted} demo workflows");
        }
        if (counts.AgentsDeleted > 0)
        {
            operations.Add($"Removed {counts.AgentsDeleted} demo agents");
        }
    }

    public async Task ReverseCatalogAndPricingAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        await _financeDbContext.CatalogBillerServices
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && item.ServiceCode.StartsWith("BILLPAY."))
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.CatalogBillers
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        var categoryCount = await _financeDbContext.CatalogBillerCategories
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && item.Name == "Utilities")
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.FxQuotes
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && item.Provider == "DemoRate")
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.FeePolicies
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && (item.Name == "BillPay-NG-GH-Default" || item.Name.StartsWith("CrossBorder-")))
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.LimitsPolicies
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && item.ScopeType == "Tenant" && item.ScopeId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        if (categoryCount > 0)
        {
            operations.Add($"Removed {categoryCount} demo catalog categories and demo pricing");
        }
    }

    public async Task ReversePartnerNetworkAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var partnerIds = await _financeDbContext.Partners
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && DemoPartnerNames.Contains(item.Name))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (partnerIds.Count == 0)
        {
            return;
        }

        var partnerFundingAccountIds = await _financeDbContext.PartnerFundingAccounts
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && partnerIds.Contains(item.PartnerId))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var ledgerAccountIds = await _financeDbContext.PartnerFundingAccounts
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && partnerIds.Contains(item.PartnerId))
            .Select(item => item.LedgerAccountId)
            .ToListAsync(cancellationToken);

        if (partnerFundingAccountIds.Count > 0)
        {
            var journalEntryIds = await _financeDbContext.JournalEntries
                .IncludeSoftDeleted()
                .Where(item => item.TenantId == tenantId && partnerFundingAccountIds.Contains(item.SourceId))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);

            if (journalEntryIds.Count > 0)
            {
                await _financeDbContext.JournalEntryLines
                    .IncludeSoftDeleted()
                    .Where(item => journalEntryIds.Contains(item.JournalEntryId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _financeDbContext.JournalEntries
                    .IncludeSoftDeleted()
                    .Where(item => journalEntryIds.Contains(item.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            await _financeDbContext.PartnerFundingAccounts
                .IncludeSoftDeleted()
                .Where(item => partnerFundingAccountIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (ledgerAccountIds.Count > 0)
        {
            await _financeDbContext.LedgerAccounts
                .IncludeSoftDeleted()
                .Where(item => ledgerAccountIds.Contains(item.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await _financeDbContext.RoutingRules
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && item.TargetPartnerId.HasValue && partnerIds.Contains(item.TargetPartnerId.Value))
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.Connectors
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && partnerIds.Contains(item.PartnerId))
            .ExecuteDeleteAsync(cancellationToken);

        await _financeDbContext.PartnerBranches
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && partnerIds.Contains(item.PartnerId))
            .ExecuteDeleteAsync(cancellationToken);

        var partnerCount = await _financeDbContext.Partners
            .IncludeSoftDeleted()
            .Where(item => partnerIds.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);

        operations.Add($"Removed {partnerCount} demo partners and routing configuration");
    }
}
