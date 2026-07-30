using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Customers;

/// <summary>
/// Spec 080 — reports the parties Finance counts as billing customers: anyone holding a customer
/// account (<c>CustomerAccount.CustomerPartyId</c>), which is what makes a party invoiceable.
/// </summary>
internal sealed class BillingCustomerRegistryContributor : ICustomerRegistryContributor
{
    private readonly FinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public BillingCustomerRegistryContributor(FinanceDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public string DomainKey => CustomerRegistryDomains.Billing;

    public async Task<IReadOnlySet<Guid>> GetParticipantsAsync(
        IReadOnlyCollection<Guid>? partyIds,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var query = _dbContext.CustomerAccounts.AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        if (partyIds is { Count: > 0 })
        {
            var ids = partyIds.Distinct().ToList();
            query = query.Where(a => ids.Contains(a.CustomerPartyId));
        }

        var found = await query
            .Select(a => a.CustomerPartyId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return found.ToHashSet();
    }
}
