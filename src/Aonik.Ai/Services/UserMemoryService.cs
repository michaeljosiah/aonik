using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

internal sealed class UserMemoryService : IUserMemoryService
{
    private const decimal ConfidenceFloor = 0.3m;
    private const decimal DecayRatePerMonth = 0.1m;

    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public UserMemoryService(
        AiDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<UserMemoryEntryResponse> SetEntryAsync(
        SetUserMemoryEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        // Find the current active entry for this key (if any) to supersede
        var existingEntry = await _dbContext.UserMemoryEntries
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId
                    && e.UserId == request.UserId
                    && e.Key == request.Key
                    && e.SupersededById == null,
                cancellationToken);

        var newEntry = new UserMemoryEntry
        {
            TenantId = tenantId,
            UserId = request.UserId,
            EntryType = request.EntryType,
            Key = request.Key,
            ValueJson = request.ValueJson,
            Confidence = request.Confidence,
            Source = request.Source,
            AiRunId = request.AiRunId,
            DecisionType = request.DecisionType,
            ConditionsJson = request.ConditionsJson,
            StaleWhen = request.StaleWhen,
            CreatedAt = now,
            LastConfirmedAt = now
        };

        _dbContext.UserMemoryEntries.Add(newEntry);

        // Supersede the old entry
        if (existingEntry is not null)
        {
            existingEntry.SupersededById = newEntry.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(newEntry, now);
    }

    public async Task<IReadOnlyList<UserMemoryEntryResponse>> GetCurrentEntriesAsync(
        Guid userId,
        UserMemoryEntryType? entryType = null,
        string? decisionType = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var query = _dbContext.UserMemoryEntries
            .Where(e => e.TenantId == tenantId
                && e.UserId == userId
                && e.SupersededById == null);

        if (entryType.HasValue)
        {
            query = query.Where(e => e.EntryType == entryType.Value);
        }

        if (!string.IsNullOrWhiteSpace(decisionType))
        {
            // Spec 041 — seek on the indexed DecisionType column instead of fetching every rationale.
            query = query.Where(e => e.DecisionType == decisionType);
        }

        var entries = await query
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Apply confidence decay and filter below floor
        return entries
            .Select(e => ToResponse(e, now))
            .Where(r => r.EffectiveConfidence >= ConfidenceFloor)
            .ToList();
    }

    public async Task<IReadOnlyList<UserMemoryEntryResponse>> GetEntryHistoryAsync(
        Guid userId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var entries = await _dbContext.UserMemoryEntries
            .Where(e => e.TenantId == tenantId && e.UserId == userId && e.Key == key)
            .OrderByDescending(e => e.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entries.Select(e => ToResponse(e, now)).ToList();
    }

    public async Task ConfirmEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.UserMemoryEntries
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);

        if (entry is null) return;

        entry.LastConfirmedAt = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<SemanticMemorySearchResult>> SemanticSearchAsync(
        Guid userId,
        string query,
        int limit = 5,
        float scoreThreshold = 0.6f,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SemanticMemorySearchResult>>(Array.Empty<SemanticMemorySearchResult>());

    private static UserMemoryEntryResponse ToResponse(UserMemoryEntry entry, DateTime now)
    {
        var effectiveConfidence = ComputeEffectiveConfidence(entry, now);

        return new UserMemoryEntryResponse(
            entry.Id,
            entry.UserId,
            entry.EntryType,
            entry.Key,
            entry.ValueJson,
            entry.Confidence,
            effectiveConfidence,
            entry.Source,
            entry.AiRunId,
            entry.SupersededById,
            entry.CreatedAt,
            entry.LastConfirmedAt,
            entry.DecisionType,
            entry.ConditionsJson,
            entry.StaleWhen);
    }

    /// <summary>
    /// User-stated entries (Confidence = 1.0) do not decay.
    /// AI-inferred entries decay: effectiveConfidence = Confidence - (daysSinceLastConfirmed / 30 * 0.1).
    /// Floor: 0.3.
    /// </summary>
    private static decimal ComputeEffectiveConfidence(UserMemoryEntry entry, DateTime now)
    {
        if (entry.Source == UserMemorySource.UserStated)
            return entry.Confidence;

        var daysSinceConfirmed = (decimal)(now - entry.LastConfirmedAt).TotalDays;
        var decay = daysSinceConfirmed / 30m * DecayRatePerMonth;
        var effective = entry.Confidence - decay;

        return Math.Max(effective, 0m);
    }
}
