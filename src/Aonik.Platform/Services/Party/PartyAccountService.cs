using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Platform.Services.Party;

internal class PartyAccountService : IPartyAccountService
{
    private readonly PlatformDbContext _dbContext;

    public PartyAccountService(PlatformDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> FindOrCreatePartyAccountAsync(
        Guid tenantId,
        Guid partyId,
        string accountType,
        string maskedIdentifier,
        string? providerRef,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.PartyAccounts
            .FirstOrDefaultAsync(
                ea => ea.TenantId == tenantId
                    && ea.PartyId == partyId
                    && ea.AccountType == accountType
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

        var account = new PartyAccount
        {
            TenantId = tenantId,
            PartyId = partyId,
            AccountType = accountType,
            MaskedIdentifier = maskedIdentifier,
            ProviderRef = providerRef,
            VerificationStatus = "Verified",
            MetadataJson = "{}"
        };

        _dbContext.PartyAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return account.Id;
    }

    public async Task<PartyAccountResult> CreatePartyAccountAsync(
        Guid tenantId,
        Guid partyId,
        string accountType,
        string maskedIdentifier,
        string? providerRef,
        string verificationStatus,
        string? currency,
        string? country,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        var account = new PartyAccount
        {
            TenantId = tenantId,
            PartyId = partyId,
            AccountType = accountType,
            MaskedIdentifier = maskedIdentifier,
            ProviderRef = providerRef,
            VerificationStatus = verificationStatus,
            Currency = currency,
            Country = country,
            MetadataJson = metadataJson ?? "{}"
        };

        _dbContext.PartyAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResult(account);
    }

    public async Task<IReadOnlyList<PartyAccountResult>> ListPartyAccountsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var accounts = await _dbContext.PartyAccounts
            .AsNoTracking()
            .Where(ea => ea.TenantId == tenantId)
            .OrderByDescending(ea => ea.CreatedAt)
            .ToListAsync(cancellationToken);

        return accounts.Select(MapToResult).ToList();
    }

    public async Task<PartyAccountResult?> GetPartyAccountAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.PartyAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                ea => ea.Id == accountId && ea.TenantId == tenantId,
                cancellationToken);

        return account == null ? null : MapToResult(account);
    }

    private static PartyAccountResult MapToResult(PartyAccount account)
    {
        return new PartyAccountResult(
            account.Id,
            account.TenantId,
            account.PartyId,
            account.AccountType,
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
