using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.PersonalFinance.Entities;
using Aonik.Finance.Persistence;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Seeding;

/// <summary>
/// Ensures every User in the platform has a corresponding PersonalProfile. This
/// is a stopgap until the Payabo onboarding flow creates profiles automatically.
///
/// Spec 027 S3 (#126): PersonalProfile is now owned solely by
/// <see cref="PersonalFinanceDbContext"/>, while the <c>Users</c> read model
/// stays on <see cref="FinanceDbContext"/>. The old single-context anti-join
/// (<c>Users.Where(u =&gt; !PersonalProfiles.Any(...))</c>) can no longer be
/// expressed in one query because the two sets live on different contexts, so
/// the difference is computed in memory: load all user keys from Finance, load
/// existing profile keys from PersonalFinance, and add the missing profiles via
/// PersonalFinance. Both contexts target the same physical database, so the
/// result is identical to the previous behaviour.
/// </summary>
internal sealed class PersonalFinanceSeedContributor : IGlobalSeedContributor
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly PersonalFinanceDbContext _personalFinanceDbContext;
    private readonly ILogger<PersonalFinanceSeedContributor> _logger;

    public PersonalFinanceSeedContributor(
        FinanceDbContext financeDbContext,
        PersonalFinanceDbContext personalFinanceDbContext,
        ILogger<PersonalFinanceSeedContributor> logger)
    {
        _financeDbContext = financeDbContext;
        _personalFinanceDbContext = personalFinanceDbContext;
        _logger = logger;
    }

    public string Key => "PersonalFinanceProfiles";
    public string DisplayName => "Personal Finance Profiles";
    public string Description => "Creates PersonalProfile records for all users that don't already have one.";
    public int SortOrder => 50;

    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var operations = new List<string>();

        // Users read model stays on FinanceDbContext.
        var users = await _financeDbContext.Users
            .Select(u => new { u.Id, u.TenantId })
            .ToListAsync(cancellationToken);

        // PersonalProfiles are now owned by PersonalFinanceDbContext.
        var existingProfileKeys = await _personalFinanceDbContext.PersonalProfiles
            .Select(p => new { p.UserId, p.TenantId })
            .ToListAsync(cancellationToken);

        var existingKeySet = existingProfileKeys
            .Select(p => (p.UserId, p.TenantId))
            .ToHashSet();

        var usersWithoutProfile = users
            .Where(u => !existingKeySet.Contains((u.Id, u.TenantId)))
            .ToList();

        if (usersWithoutProfile.Count == 0)
        {
            operations.Add("All users already have PersonalProfile records.");
            return operations;
        }

        foreach (var user in usersWithoutProfile)
        {
            _personalFinanceDbContext.PersonalProfiles.Add(new PersonalProfile
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                PartyId = Guid.Empty
            });

            _logger.LogInformation(
                "Created PersonalProfile for User {UserId} in Tenant {TenantId}",
                user.Id, user.TenantId);

            operations.Add($"Created PersonalProfile for user {user.Id}");
        }

        await _personalFinanceDbContext.SaveChangesAsync(cancellationToken);
        return operations;
    }
}
