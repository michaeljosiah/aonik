using Microsoft.EntityFrameworkCore;

using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Aonik.SharedKernel.Persistence;

namespace Aonik.PersonalFinance.Services.Seeding;

/// <summary>
/// PersonalFinance-side implementation of the demo-data teardown port
/// (<see cref="IPersonalFinanceDemoDataReverser"/>). Owns the ExecuteDelete logic
/// over <see cref="PersonalFinanceDbContext"/> — previously inlined in Platform's
/// <c>ReverseSeedPhase</c> against <c>FinanceDbContext</c>, before the PF DbSets
/// moved to PersonalFinance (Spec 027 S3, #126). Soft-deleted rows are included so
/// teardown is exhaustive.
/// </summary>
internal sealed class PersonalFinanceDemoDataReverser : IPersonalFinanceDemoDataReverser
{
    private readonly PersonalFinanceDbContext _dbContext;

    public PersonalFinanceDemoDataReverser(PersonalFinanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PersonalFinanceDemoReversalCounts> ReversePersonaActivityAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> personaUserIds,
        CancellationToken cancellationToken = default)
    {
        var txCount = await _dbContext.PersonalTransactions
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && personaUserIds.Contains(item.UserId))
            .ExecuteDeleteAsync(cancellationToken);

        var billCount = await _dbContext.PersonalRecurringBills
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && personaUserIds.Contains(item.UserId))
            .ExecuteDeleteAsync(cancellationToken);

        var subCount = await _dbContext.Subscriptions
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && personaUserIds.Contains(item.UserId))
            .ExecuteDeleteAsync(cancellationToken);

        var accountCount = await _dbContext.PersonalAccounts
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && personaUserIds.Contains(item.UserId))
            .ExecuteDeleteAsync(cancellationToken);

        var profileCount = await _dbContext.PersonalProfiles
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && personaUserIds.Contains(item.UserId))
            .ExecuteDeleteAsync(cancellationToken);

        return new PersonalFinanceDemoReversalCounts(
            txCount, billCount, subCount, accountCount, profileCount);
    }

    public async Task<int> ReverseHouseholdsAsync(
        Guid tenantId,
        IReadOnlyCollection<string> householdNames,
        CancellationToken cancellationToken = default)
    {
        var householdIds = await _dbContext.Households
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && householdNames.Contains(item.Name))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        if (householdIds.Count == 0)
        {
            return 0;
        }

        await _dbContext.HouseholdMembers
            .IncludeSoftDeleted()
            .Where(item => householdIds.Contains(item.HouseholdId))
            .ExecuteDeleteAsync(cancellationToken);

        return await _dbContext.Households
            .IncludeSoftDeleted()
            .Where(item => householdIds.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
