using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Customers;

/// <summary>
/// Spec 080 — reports the parties Commerce counts as storefront customers: anyone with a cart
/// bound to them (<c>Cart.BuyerPartyId</c>). The cart is the right marker rather than the order,
/// because it is Commerce's own ownership record and it covers both a live box session and every
/// order that session produced — a customer who is mid-build is already a storefront customer.
/// </summary>
internal sealed class StorefrontCustomerRegistryContributor : ICustomerRegistryContributor
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public StorefrontCustomerRegistryContributor(CommerceDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public string DomainKey => CustomerRegistryDomains.Storefront;

    public async Task<IReadOnlySet<Guid>> GetParticipantsAsync(
        IReadOnlyCollection<Guid>? partyIds,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var query = _dbContext.Carts.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.BuyerPartyId != null);

        // Null/empty asks for every participant — the shape the domain= filter needs, since it
        // must narrow the registry BEFORE paging.
        if (partyIds is { Count: > 0 })
        {
            var ids = partyIds.Distinct().ToList();
            query = query.Where(c => ids.Contains(c.BuyerPartyId!.Value));
        }

        var found = await query
            .Select(c => c.BuyerPartyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        return found.ToHashSet();
    }
}
