using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class PersonalProfileProvisioner : IPersonalProfileProvisioner
{
    private readonly FinanceDbContext _dbContext;
    private readonly ILogger<PersonalProfileProvisioner> _logger;

    public PersonalProfileProvisioner(
        FinanceDbContext dbContext,
        ILogger<PersonalProfileProvisioner> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task EnsurePersonalProfileAsync(
        Guid tenantId,
        Guid userId,
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.PersonalProfiles
            .AnyAsync(p => p.UserId == userId && p.TenantId == tenantId, cancellationToken);

        if (exists)
        {
            return;
        }

        _dbContext.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created PersonalProfile for User {UserId} in Tenant {TenantId}",
            userId, tenantId);
    }
}
