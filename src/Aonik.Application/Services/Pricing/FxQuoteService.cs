using Microsoft.EntityFrameworkCore;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Pricing;
using Aonik.Domain.Pricing.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Pricing;

public class FxQuoteService : IFxQuoteService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public FxQuoteService(
        IAonikDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<IReadOnlyCollection<FxQuoteListResponse>> GetAllAsync(
        string? baseCurrency = null,
        string? targetCurrency = null,
        bool includeExpired = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var query = _dbContext.FxQuotes
            .Where(q => q.TenantId == tenantId);

        if (!string.IsNullOrEmpty(baseCurrency))
        {
            query = query.Where(q => q.BaseCurrency == baseCurrency);
        }

        if (!string.IsNullOrEmpty(targetCurrency))
        {
            query = query.Where(q => q.TargetCurrency == targetCurrency);
        }

        if (!includeExpired)
        {
            query = query.Where(q => q.ExpiresAt >= now);
        }

        var quotes = await query
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(cancellationToken);

        return quotes.Select(MapToListResponse).ToList();
    }

    public async Task<FxQuoteDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var quote = await _dbContext.FxQuotes
            .FirstOrDefaultAsync(q => q.Id == id && q.TenantId == tenantId, cancellationToken);

        return quote == null ? null : MapToDetailResponse(quote);
    }

    public async Task<FxQuoteDetailResponse> CreateAsync(
        CreateFxQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var quote = new FxQuote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = request.BaseCurrency,
            TargetCurrency = request.TargetCurrency,
            Rate = request.Rate,
            ExpiresAt = request.ExpiresAt,
            Provider = request.Provider,
            MetadataJson = request.MetadataJson ?? string.Empty,
            CreatedAt = _clock.UtcNow
        };

        _dbContext.FxQuotes.Add(quote);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDetailResponse(quote);
    }

    public async Task<FxQuoteDetailResponse> UpdateAsync(
        Guid id,
        UpdateFxQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var quote = await _dbContext.FxQuotes
            .FirstOrDefaultAsync(q => q.Id == id && q.TenantId == tenantId, cancellationToken);

        if (quote == null)
        {
            throw new InvalidOperationException($"FX quote {id} not found");
        }

        quote.Rate = request.Rate;
        quote.ExpiresAt = request.ExpiresAt;
        quote.Provider = request.Provider;
        quote.MetadataJson = request.MetadataJson ?? string.Empty;
        quote.UpdatedAt = _clock.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToDetailResponse(quote);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var quote = await _dbContext.FxQuotes
            .FirstOrDefaultAsync(q => q.Id == id && q.TenantId == tenantId, cancellationToken);

        if (quote == null)
        {
            throw new InvalidOperationException($"FX quote {id} not found");
        }

        _dbContext.FxQuotes.Remove(quote);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static FxQuoteListResponse MapToListResponse(FxQuote quote)
    {
        return new FxQuoteListResponse(
            quote.Id,
            quote.BaseCurrency,
            quote.TargetCurrency,
            quote.Rate,
            quote.ExpiresAt,
            quote.Provider,
            quote.CreatedAt,
            quote.UpdatedAt);
    }

    private static FxQuoteDetailResponse MapToDetailResponse(FxQuote quote)
    {
        return new FxQuoteDetailResponse(
            quote.Id,
            quote.TenantId,
            quote.BaseCurrency,
            quote.TargetCurrency,
            quote.Rate,
            quote.ExpiresAt,
            quote.Provider,
            quote.MetadataJson,
            quote.CreatedAt,
            quote.UpdatedAt);
    }
}
