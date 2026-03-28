using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Party;

internal class ExternalAccountService : IExternalAccountService
{
    private readonly PlatformDbContext _dbContext;

    public ExternalAccountService(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> FindOrCreateExternalAccountAsync(
        Guid tenantId,
        Guid partyId,
        string externalAccountType,
        string maskedIdentifier,
        string? providerRef,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ExternalAccounts
            .FirstOrDefaultAsync(
                ea => ea.TenantId == tenantId
                    && ea.PartyId == partyId
                    && ea.ExternalAccountType == externalAccountType
                    && ea.MaskedIdentifier == maskedIdentifier,
                cancellationToken);

        if (existing != null)
        {
            if (providerRef != null && existing.ProviderRef != providerRef)
            {
                existing.ProviderRef = providerRef;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return existing.Id;
        }

        var account = new ExternalAccount
        {
            TenantId = tenantId,
            PartyId = partyId,
            ExternalAccountType = externalAccountType,
            MaskedIdentifier = maskedIdentifier,
            ProviderRef = providerRef,
            VerificationStatus = "Verified",
            MetadataJson = "{}"
        };

        _dbContext.ExternalAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return account.Id;
    }

    public async Task<ExternalAccountResult> CreateExternalAccountAsync(
        Guid tenantId,
        Guid partyId,
        string externalAccountType,
        string maskedIdentifier,
        string? providerRef,
        string verificationStatus,
        string? currency,
        string? country,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        var account = new ExternalAccount
        {
            TenantId = tenantId,
            PartyId = partyId,
            ExternalAccountType = externalAccountType,
            MaskedIdentifier = maskedIdentifier,
            ProviderRef = providerRef,
            VerificationStatus = verificationStatus,
            Currency = currency,
            Country = country,
            MetadataJson = metadataJson ?? "{}"
        };

        _dbContext.ExternalAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResult(account);
    }

    public async Task<IReadOnlyList<ExternalAccountResult>> ListExternalAccountsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _dbContext.ExternalAccounts
            .AsNoTracking()
            .Where(ea => ea.TenantId == tenantId)
            .OrderByDescending(ea => ea.CreatedAt)
            .ToListAsync(cancellationToken);

        return accounts.Select(MapToResult).ToList();
    }

    public async Task<ExternalAccountResult?> GetExternalAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.ExternalAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                ea => ea.Id == accountId && ea.TenantId == tenantId,
                cancellationToken);

        return account == null ? null : MapToResult(account);
    }

    private static ExternalAccountResult MapToResult(ExternalAccount account)
    {
        return new ExternalAccountResult(
            account.Id,
            account.TenantId,
            account.PartyId,
            account.ExternalAccountType,
            account.MaskedIdentifier,
            account.ProviderRef,
            account.VerificationStatus,
            account.Currency,
            account.Country,
            account.MetadataJson,
            account.CreatedAt,
            account.UpdatedAt);
    }
}
