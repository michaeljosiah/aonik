using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;

namespace Aonik.PersonalFinance.Services.Seeding;

/// <summary>
/// Ensures every User in the platform has a corresponding PersonalProfile. This
/// is a stopgap until the Payabo onboarding flow creates profiles automatically.
///
/// Spec 027 S5 (#118/#126): the contributor now lives in PersonalFinance and no
/// longer references <c>FinanceDbContext</c>. The <c>Users</c> read model is
/// read through the SharedKernel <see cref="IUserDirectoryReader"/> port
/// (implemented over Platform's Users) with the ambient tenant filter applied —
/// a behaviour-preserving port of the previous direct FinanceDbContext.Users
/// read. PersonalProfile is owned
/// solely by <see cref="PersonalFinanceDbContext"/>, so the anti-join can't be
/// expressed in one query; the difference is computed in memory: load all user
/// keys via the reader, load existing profile keys from PersonalFinance, and add
/// the missing profiles via PersonalFinance.
/// </summary>
internal sealed class PersonalFinanceSeedContributor : IGlobalSeedContributor
{
    private readonly IUserDirectoryReader _userDirectoryReader;
    private readonly PersonalFinanceDbContext _personalFinanceDbContext;
    private readonly ILogger<PersonalFinanceSeedContributor> _logger;

    public PersonalFinanceSeedContributor(
        IUserDirectoryReader userDirectoryReader,
        PersonalFinanceDbContext personalFinanceDbContext,
        ILogger<PersonalFinanceSeedContributor> logger)
    {
        _userDirectoryReader = userDirectoryReader;
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

        // Users read model read through the SharedKernel port.
        var users = await _userDirectoryReader.GetAllUserKeysAsync(cancellationToken);

        // PersonalProfiles are owned by PersonalFinanceDbContext.
        var existingProfileKeys = await _personalFinanceDbContext.PersonalProfiles
            .Select(p => new { p.UserId, p.TenantId })
            .ToListAsync(cancellationToken);

        var existingKeySet = existingProfileKeys
            .Select(p => (p.UserId, p.TenantId))
            .ToHashSet();

        var usersWithoutProfile = users
            .Where(u => !existingKeySet.Contains((u.UserId, u.TenantId)))
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
                UserId = user.UserId,
                PartyId = Guid.Empty
            });

            _logger.LogInformation(
                "Created PersonalProfile for User {UserId} in Tenant {TenantId}",
                user.UserId, user.TenantId);

            operations.Add($"Created PersonalProfile for user {user.UserId}");
        }

        await _personalFinanceDbContext.SaveChangesAsync(cancellationToken);
        return operations;
    }
}
