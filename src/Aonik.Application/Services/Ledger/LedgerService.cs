using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Ledger;
using Aonik.Application.Services.Identity;
using Aonik.Domain.Ledger.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Ledger;

public class LedgerService : ILedgerService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserProvider _currentUserProvider;

    public LedgerService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IPermissionService permissionService,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<LedgerAccountResponse> CreateAccountAsync(CreateLedgerAccountRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Ledger.Write", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var account = new LedgerAccount
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Code = string.Empty, // TODO: Generate account code based on business rules
            AccountType = "General", // TODO: Add AccountType to request or infer from business rules
            LedgerId = Guid.Empty, // TODO: Add LedgerId to request or get from context
            TenantId = tenantId,
            DimensionsJson = "{}"
        };

        _dbContext.LedgerAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LedgerAccountResponse(
            account.Id,
            account.Name,
            "N/A", // Currency is not a property on LedgerAccount entity
            account.CreatedAt);
    }

    public async Task<JournalEntryResponse> AddJournalEntryAsync(AddJournalEntryRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Ledger.Write", cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            LedgerId = Guid.Empty, // TODO: Add LedgerId to request or get from context
            TenantId = tenantId,
            Timestamp = DateTime.UtcNow,
            SourceType = "Manual", // TODO: Add SourceType to request or determine from context
            SourceId = request.AccountId,
            Status = "Posted",
            Lines = new List<JournalEntryLine>() // TODO: Map from request or create lines separately
        };

        _dbContext.JournalEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new JournalEntryResponse(
            entry.Id,
            request.AccountId,
            request.Amount,
            request.Currency,
            entry.Timestamp,
            request.Reference,
            request.Description);
    }

    private async Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, permissionKey, cancellationToken);
        if (!hasPermission)
        {
            throw new InvalidOperationException($"Permission {permissionKey} is required.");
        }
    }
}
