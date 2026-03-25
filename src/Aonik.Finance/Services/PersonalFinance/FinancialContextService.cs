using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.PersonalFinance;

internal sealed class FinancialContextService : IFinancialContextService
{
    private readonly FinanceDbContext _financeDbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;

    public FinancialContextService(
        FinanceDbContext financeDbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator)
    {
        _financeDbContext = financeDbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task<FinancialContextResponse> CreateContextAsync(
        CreateFinancialContextRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Name, nameof(request.Name));
        ValidateRequiredText(request.ContextType, nameof(request.ContextType));

        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var context = new FinancialContext
        {
            TenantId = tenantId,
            UserId = userId,
            Name = request.Name.Trim(),
            ContextType = request.ContextType.Trim(),
            RelatedPartyId = request.RelatedPartyId,
            Status = "Active",
            Notes = TrimNullable(request.Notes),
            MetadataJson = request.MetadataJson ?? "{}"
        };

        _financeDbContext.FinancialContexts.Add(context);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

        return MapToResponse(context);
    }

    public async Task<IReadOnlyList<FinancialContextResponse>> ListContextsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var query = _financeDbContext.FinancialContexts
            .AsNoTracking()
            .Include(c => c.FundingSources)
            .Where(c => c.TenantId == tenantId && c.UserId == userId);

        if (!includeArchived)
        {
            query = query.Where(c => c.Status != "Archived");
        }

        var contexts = await query
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return contexts.Select(MapToResponse).ToList();
    }

    public async Task<FinancialContextResponse?> GetContextAsync(
        Guid contextId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetOwnedContextWithSourcesAsync(contextId, cancellationToken);
        return context == null ? null : MapToResponse(context);
    }

    public async Task<FinancialContextResponse> UpdateContextAsync(
        Guid contextId,
        UpdateFinancialContextRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequiredText(request.Name, nameof(request.Name));
        ValidateRequiredText(request.ContextType, nameof(request.ContextType));

        var context = await GetOwnedContextWithSourcesAsync(contextId, cancellationToken)
            ?? throw new InvalidOperationException("Financial context not found.");

        context.Name = request.Name.Trim();
        context.ContextType = request.ContextType.Trim();
        context.RelatedPartyId = request.RelatedPartyId;
        context.Notes = TrimNullable(request.Notes);
        context.MetadataJson = request.MetadataJson ?? "{}";

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);

        return MapToResponse(context);
    }

    public async Task ArchiveContextAsync(
        Guid contextId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetOwnedContextAsync(contextId, cancellationToken)
            ?? throw new InvalidOperationException("Financial context not found.");

        context.Status = "Archived";

        await _financeDbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidator.InvalidateCurrentUserGraphAsync(cancellationToken);
    }

    public async Task<FinancialContextFundingSourceResponse> AddFundingSourceAsync(
        Guid contextId,
        AddFundingSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = await GetOwnedContextAsync(contextId, cancellationToken)
            ?? throw new InvalidOperationException("Financial context not found.");

        // Verify the account belongs to the same user
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var accountExists = await _financeDbContext.PersonalAccounts
            .AnyAsync(a => a.Id == request.PersonalAccountId
                && a.TenantId == tenantId
                && a.UserId == userId,
                cancellationToken);

        if (!accountExists)
        {
            throw new InvalidOperationException("Personal account not found.");
        }

        // Check for duplicate
        var alreadyLinked = await _financeDbContext.FinancialContextFundingSources
            .AnyAsync(fs => fs.FinancialContextId == contextId
                && fs.PersonalAccountId == request.PersonalAccountId,
                cancellationToken);

        if (alreadyLinked)
        {
            throw new ArgumentException("This account is already linked to the context.");
        }

        // If marking as primary, unset existing primary
        if (request.IsPrimary)
        {
            var existingPrimary = await _financeDbContext.FinancialContextFundingSources
                .Where(fs => fs.FinancialContextId == contextId && fs.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var fs in existingPrimary)
            {
                fs.IsPrimary = false;
            }
        }

        var fundingSource = new FinancialContextFundingSource
        {
            TenantId = context.TenantId,
            FinancialContextId = contextId,
            PersonalAccountId = request.PersonalAccountId,
            IsPrimary = request.IsPrimary
        };

        _financeDbContext.FinancialContextFundingSources.Add(fundingSource);
        await _financeDbContext.SaveChangesAsync(cancellationToken);

        return MapFundingSourceToResponse(fundingSource);
    }

    public async Task RemoveFundingSourceAsync(
        Guid contextId,
        Guid fundingSourceId,
        CancellationToken cancellationToken = default)
    {
        _ = await GetOwnedContextAsync(contextId, cancellationToken)
            ?? throw new InvalidOperationException("Financial context not found.");

        var fundingSource = await _financeDbContext.FinancialContextFundingSources
            .FirstOrDefaultAsync(fs => fs.Id == fundingSourceId && fs.FinancialContextId == contextId,
                cancellationToken)
            ?? throw new InvalidOperationException("Funding source not found.");

        _financeDbContext.FinancialContextFundingSources.Remove(fundingSource);
        await _financeDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignTransactionContextAsync(
        Guid transactionId,
        AssignTransactionContextRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var transaction = await _financeDbContext.PersonalTransactions
            .FirstOrDefaultAsync(t => t.Id == transactionId
                && t.TenantId == tenantId
                && t.UserId == userId,
                cancellationToken)
            ?? throw new InvalidOperationException("Personal transaction not found.");

        // If assigning (not unassigning), verify the context belongs to same user
        if (request.FinancialContextId.HasValue)
        {
            var contextExists = await _financeDbContext.FinancialContexts
                .AnyAsync(c => c.Id == request.FinancialContextId.Value
                    && c.TenantId == tenantId
                    && c.UserId == userId,
                    cancellationToken);

            if (!contextExists)
            {
                throw new InvalidOperationException("Financial context not found.");
            }
        }

        transaction.FinancialContextId = request.FinancialContextId;
        await _financeDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<FinancialContextSummaryResponse> GetContextSummaryAsync(
        Guid contextId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var context = await GetOwnedContextAsync(contextId, cancellationToken)
            ?? throw new InvalidOperationException("Financial context not found.");

        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var periodStart = from ?? DateTime.UtcNow.AddDays(-30);
        var periodEnd = to ?? DateTime.UtcNow;

        var query = _financeDbContext.PersonalTransactions
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                && t.UserId == userId
                && t.FinancialContextId == contextId
                && t.OccurredAt >= periodStart
                && t.OccurredAt <= periodEnd);

        var transactions = await query.ToListAsync(cancellationToken);

        var totalInflow = transactions
            .Where(t => t.Amount > 0)
            .Sum(t => t.Amount);

        var totalOutflow = transactions
            .Where(t => t.Amount < 0)
            .Sum(t => Math.Abs(t.Amount));

        return new FinancialContextSummaryResponse(
            context.Id,
            context.Name,
            context.ContextType,
            totalInflow,
            totalOutflow,
            totalInflow - totalOutflow,
            transactions.Count,
            periodStart,
            periodEnd);
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private async Task<FinancialContext?> GetOwnedContextAsync(
        Guid contextId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _financeDbContext.FinancialContexts
            .FirstOrDefaultAsync(
                c => c.Id == contextId && c.TenantId == tenantId && c.UserId == userId,
                cancellationToken);
    }

    private async Task<FinancialContext?> GetOwnedContextWithSourcesAsync(
        Guid contextId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _financeDbContext.FinancialContexts
            .Include(c => c.FundingSources)
            .FirstOrDefaultAsync(
                c => c.Id == contextId && c.TenantId == tenantId && c.UserId == userId,
                cancellationToken);
    }

    private static FinancialContextResponse MapToResponse(FinancialContext context)
    {
        return new FinancialContextResponse(
            context.Id,
            context.UserId,
            context.Name,
            context.ContextType,
            context.RelatedPartyId,
            context.Status,
            context.Notes,
            context.MetadataJson,
            context.FundingSources?.Select(MapFundingSourceToResponse).ToList()
                ?? new List<FinancialContextFundingSourceResponse>(),
            context.CreatedAt,
            context.UpdatedAt);
    }

    private static FinancialContextFundingSourceResponse MapFundingSourceToResponse(
        FinancialContextFundingSource source)
    {
        return new FinancialContextFundingSourceResponse(
            source.Id,
            source.FinancialContextId,
            source.PersonalAccountId,
            source.IsPrimary,
            source.CreatedAt);
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
}
