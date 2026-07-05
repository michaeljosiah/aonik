using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

internal sealed class PersonalAccountService : IPersonalAccountService
{
    private readonly PersonalFinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public PersonalAccountService(
        PersonalFinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<PersonalAccountResponse> CreateAccountAsync(
        CreatePersonalAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Name, nameof(request.Name));
        ValidateRequiredText(request.AccountType, nameof(request.AccountType));
        ValidateRequiredText(request.Currency, nameof(request.Currency));

        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var account = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = userId,
            Name = request.Name.Trim(),
            AccountType = request.AccountType.Trim(),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            InstitutionName = TrimNullable(request.InstitutionName),
            ExternalReference = TrimNullable(request.ExternalReference),
            Status = "Active",
            AccountSubtype = TrimNullable(request.AccountSubtype),
            Last4 = NormalizeLast4(request.Last4),
            CurrentBalance = request.StartingBalance ?? 0m,
            BalanceAsOf = request.StartingBalance.HasValue ? DateTime.UtcNow : null,
            IsArchived = false,
            OpenedAt = DateTime.UtcNow
        };

        _financeDbContext.PersonalAccounts.Add(account);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

        return MapToResponse(account);
    }

    public async Task<IReadOnlyList<PersonalAccountResponse>> ListAccountsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _financeDbContext.PersonalAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && account.UserId == userId);

        if (!includeArchived)
        {
            query = query.Where(account => !account.IsArchived);
        }

        var accounts = await query
            .OrderBy(account => account.Name)
            .ToListAsync(cancellationToken);

        return accounts.Select(MapToResponse).ToList();
    }

    public async Task<PersonalAccountResponse?> GetAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await GetOwnedAccountAsync(accountId, cancellationToken);
        return account == null ? null : MapToResponse(account);
    }

    public async Task<PersonalAccountResponse> UpdateAccountAsync(
        Guid accountId,
        UpdatePersonalAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Name, nameof(request.Name));
        ValidateRequiredText(request.AccountType, nameof(request.AccountType));
        ValidateRequiredText(request.Currency, nameof(request.Currency));
        ValidateRequiredText(request.Status, nameof(request.Status));

        var account = await GetOwnedAccountAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException("Personal account not found.");

        account.Name = request.Name.Trim();
        account.AccountType = request.AccountType.Trim();
        account.Currency = request.Currency.Trim().ToUpperInvariant();
        account.InstitutionName = TrimNullable(request.InstitutionName);
        account.ExternalReference = TrimNullable(request.ExternalReference);
        account.AccountSubtype = TrimNullable(request.AccountSubtype);
        account.Last4 = NormalizeLast4(request.Last4);
        account.Status = request.Status.Trim();

        if (request.CurrentBalance.HasValue)
        {
            var isLinkedAccount = await _financeDbContext.PersonalLinkedAccounts
                .AnyAsync(item => item.PersonalAccountId == account.Id, cancellationToken);

            if (isLinkedAccount)
            {
                throw new ArgumentException("CurrentBalance can only be set for manual accounts.", nameof(request.CurrentBalance));
            }

            account.CurrentBalance = request.CurrentBalance.Value;
            account.BalanceAsOf = DateTime.UtcNow;
        }

        if (account.IsArchived)
        {
            account.ClosedAt ??= DateTime.UtcNow;
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
        return MapToResponse(account);
    }

    public async Task ArchiveAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await GetOwnedAccountAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException("Personal account not found.");

        account.IsArchived = true;
        account.Status = "Archived";
        account.ClosedAt ??= DateTime.UtcNow;

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
    }

    public async Task DeleteManualAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await GetOwnedAccountAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException("Personal account not found.");

        var isLinkedAccount = await _financeDbContext.PersonalLinkedAccounts
            .AnyAsync(item => item.PersonalAccountId == account.Id, cancellationToken);

        if (isLinkedAccount)
        {
            throw new ArgumentException(
                "Linked accounts cannot be deleted. Disconnect the account instead.");
        }

        // Soft-delete all transactions belonging to this manual account.
        var transactions = await _financeDbContext.PersonalTransactions
            .Where(t => t.PersonalAccountId == account.Id && t.TenantId == account.TenantId && t.UserId == account.UserId)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            _financeDbContext.PersonalTransactions.Remove(transaction);
        }

        // Remove statement imports associated with this account.
        var statementImports = await _financeDbContext.StatementImports
            .Where(si => si.PersonalAccountId == account.Id && si.TenantId == account.TenantId)
            .ToListAsync(cancellationToken);

        foreach (var import in statementImports)
        {
            _financeDbContext.StatementImports.Remove(import);
        }

        // Remove financial context funding sources referencing this account.
        var fundingSources = await _financeDbContext.FinancialContextFundingSources
            .Where(fs => fs.PersonalAccountId == account.Id)
            .ToListAsync(cancellationToken);

        foreach (var fundingSource in fundingSources)
        {
            _financeDbContext.FinancialContextFundingSources.Remove(fundingSource);
        }

        _financeDbContext.PersonalAccounts.Remove(account);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
    }

    public async Task<PersonalAccountResponse> ShareAccountWithHouseholdAsync(
        Guid accountId,
        ShareAccountWithHouseholdRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.HouseholdId == Guid.Empty)
        {
            throw new ArgumentException("HouseholdId is required.", nameof(request.HouseholdId));
        }

        var account = await GetOwnedAccountAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException("Personal account not found.");

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var memberships = await _financeDbContext.HouseholdMembers
            .Where(member => member.TenantId == tenantId
                && member.HouseholdId == request.HouseholdId
                && member.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var membership in memberships)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(membership);
        }

        var acceptedMembership = memberships.FirstOrDefault(HouseholdMembershipRules.IsAccepted)
            ?? throw new InvalidOperationException("Current user is not an accepted member of this household.");

        if (!HouseholdMembershipRules.CanManageMembers(acceptedMembership))
        {
            throw new UnauthorizedAccessException("Only household owners or managers can share accounts with the household.");
        }

        account.HouseholdId = request.HouseholdId;

        _financeDbContext.EnqueueIntegrationEvent(new HouseholdAccountSharedEvent(tenantId, request.HouseholdId, account.Id));

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        var acceptedMembers = await _financeDbContext.HouseholdMembers
            .Where(member => member.TenantId == tenantId && member.HouseholdId == request.HouseholdId)
            .ToListAsync(cancellationToken);

        foreach (var membership in acceptedMembers)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(membership);
        }

        await _cacheInvalidator.InvalidateUserGraphsAsync(
            acceptedMembers.Where(HouseholdMembershipRules.IsAccepted).Select(item => item.UserId).Append(userId).Distinct(),
            cancellationToken);

        return MapToResponse(account);
    }

    public async Task<PersonalAccountResponse> UnshareAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await GetOwnedAccountAsync(accountId, cancellationToken)
            ?? throw new InvalidOperationException("Personal account not found.");

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var householdId = account.HouseholdId;

        account.HouseholdId = null;

        if (householdId.HasValue)
        {
            _financeDbContext.EnqueueIntegrationEvent(new HouseholdAccountUnsharedEvent(tenantId, householdId.Value, account.Id));
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);

        if (householdId.HasValue)
        {
            var acceptedMembers = await _financeDbContext.HouseholdMembers
                .Where(member => member.TenantId == tenantId && member.HouseholdId == householdId.Value)
                .ToListAsync(cancellationToken);

            foreach (var membership in acceptedMembers)
            {
                HouseholdMembershipRules.NormalizeLegacyMember(membership);
            }

            await _cacheInvalidator.InvalidateUserGraphsAsync(
                acceptedMembers.Where(HouseholdMembershipRules.IsAccepted).Select(item => item.UserId).Append(userId).Distinct(),
                cancellationToken);
        }
        else
        {
            await _cacheInvalidator.InvalidateUserGraphAsync(userId, cancellationToken);
        }

        return MapToResponse(account);
    }

    public async Task<IReadOnlyList<PersonalAccountResponse>> ListHouseholdAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var memberships = await _financeDbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var membership in memberships)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(membership);
        }

        var acceptedMembership = memberships.FirstOrDefault(HouseholdMembershipRules.IsAccepted)
            ?? throw new InvalidOperationException("Current user does not belong to an accepted household.");

        var accounts = await _financeDbContext.PersonalAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId && account.HouseholdId == acceptedMembership.HouseholdId)
            .OrderBy(account => account.Name)
            .ToListAsync(cancellationToken);

        return accounts.Select(MapToResponse).ToList();
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private async Task<PersonalAccount?> GetOwnedAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _financeDbContext.PersonalAccounts
            .FirstOrDefaultAsync(
                account => account.Id == accountId && account.TenantId == tenantId && account.UserId == userId,
                cancellationToken);
    }

    private static PersonalAccountResponse MapToResponse(PersonalAccount account)
    {
        return new PersonalAccountResponse(
            account.Id,
            account.UserId,
            account.HouseholdId,
            account.Name,
            account.AccountType,
            account.Currency,
            account.InstitutionName,
            account.ExternalReference,
            account.Status,
            account.AccountSubtype,
            account.Last4,
            account.CurrentBalance,
            account.BalanceAsOf,
            account.IsArchived,
            account.OpenedAt,
            account.ClosedAt,
            account.CreatedAt,
            account.UpdatedAt);
    }

    private static void ValidateRequiredText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }
    }

    private static string? TrimNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeLast4(string? last4)
    {
        var normalized = TrimNullable(last4);
        if (normalized == null)
        {
            return null;
        }

        if (normalized.Length > 4)
        {
            throw new ArgumentException("Last4 cannot exceed 4 characters.", nameof(last4));
        }

        return normalized;
    }
}
