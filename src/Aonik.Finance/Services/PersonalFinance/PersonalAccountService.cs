using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class PersonalAccountService : IPersonalAccountService
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PersonalAccountService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
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
            IsArchived = false,
            OpenedAt = DateTime.UtcNow
        };

        _financeDbContext.PersonalAccounts.Add(account);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

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

        if (account.IsArchived)
        {
            account.ClosedAt ??= DateTime.UtcNow;
        }

        await _financeDbContext.SaveChangesAsync(cancellationToken);
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
