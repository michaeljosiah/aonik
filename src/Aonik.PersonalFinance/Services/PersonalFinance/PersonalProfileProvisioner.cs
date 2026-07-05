using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions.PersonalFinance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.PersonalFinance.Services;

internal sealed class PersonalProfileProvisioner : IPersonalProfileProvisioner
{
    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ILogger<PersonalProfileProvisioner> _logger;

    public PersonalProfileProvisioner(
        PersonalFinanceDbContext dbContext,
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
