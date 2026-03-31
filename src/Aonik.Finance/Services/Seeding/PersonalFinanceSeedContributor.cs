using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Seeding;

/// <summary>
/// Ensures every User in the platform has a corresponding PersonalProfile in the
/// Finance module. This is a stopgap until the Payabo onboarding flow creates
/// profiles automatically.
/// </summary>
internal sealed class PersonalFinanceSeedContributor : IGlobalSeedContributor
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ILogger<PersonalFinanceSeedContributor> _logger;

    public PersonalFinanceSeedContributor(
        FinanceDbContext financeDbContext,
        ILogger<PersonalFinanceSeedContributor> logger)
    {
        _financeDbContext = financeDbContext;
        _logger = logger;
    }

    public string Key => "PersonalFinanceProfiles";
    public string DisplayName => "Personal Finance Profiles";
    public string Description => "Creates PersonalProfile records for all users that don't already have one.";
    public int SortOrder => 50;

    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var operations = new List<string>();

        var usersWithoutProfile = await _financeDbContext.Users
            .Where(u => !_financeDbContext.PersonalProfiles
                .Any(p => p.UserId == u.Id && p.TenantId == u.TenantId))
            .Select(u => new { u.Id, u.TenantId })
            .ToListAsync(cancellationToken);

        if (usersWithoutProfile.Count == 0)
        {
            operations.Add("All users already have PersonalProfile records.");
            return operations;
        }

        foreach (var user in usersWithoutProfile)
        {
            _financeDbContext.PersonalProfiles.Add(new PersonalProfile
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

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        return operations;
    }
}
