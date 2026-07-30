using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services.Customers;

/// <summary>
/// Spec 080 — reports the parties enrolled in PersonalFinance: those with a personal profile
/// (<c>PersonalProfile.PartyId</c>). Households and accounts are keyed by user rather than party,
/// so the profile is the one party-keyed enrolment record — and it is the right one, since it is
/// what enrolment creates.
/// </summary>
internal sealed class PersonalFinanceCustomerRegistryContributor : ICustomerRegistryContributor
{
    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public PersonalFinanceCustomerRegistryContributor(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public string DomainKey => CustomerRegistryDomains.PersonalFinance;

    public async Task<IReadOnlySet<Guid>> GetParticipantsAsync(
        IReadOnlyCollection<Guid>? partyIds,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var query = _dbContext.PersonalProfiles.AsNoTracking()
            .Where(p => p.TenantId == tenantId);

        if (partyIds is { Count: > 0 })
        {
            var ids = partyIds.Distinct().ToList();
            query = query.Where(p => ids.Contains(p.PartyId));
        }

        var found = await query
            .Select(p => p.PartyId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return found.ToHashSet();
    }
}
