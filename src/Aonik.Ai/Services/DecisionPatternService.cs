using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

/// <summary>
/// SQL-first tenant pattern store (Spec 041, Addition B). Reinforces a current pattern on a confirming
/// outcome and supersedes-then-restarts on a contradicting one. Tenant isolation is enforced by the
/// AiDbContext query filter plus the TenantId stamped on every write.
/// </summary>
internal sealed class DecisionPatternService : IDecisionPatternService
{
    private const decimal SeedConfidence = 0.5m;
    private const decimal ContradictionSeedConfidence = 0.4m;
    private const decimal MaxConfidence = 0.99m;
    private const decimal ReinforceStep = 0.2m;

    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public DecisionPatternService(AiDbContext dbContext, ITenantProvider tenantProvider, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<IReadOnlyList<DecisionPatternView>> GetTopPatternsAsync(
        string decisionType,
        string? segment = null,
        int limit = 3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(decisionType))
        {
            return [];
        }

        var type = decisionType.Trim();
        var seg = Normalize(segment);
        limit = Math.Clamp(limit, 1, 25);

        var query = _dbContext.DecisionPatterns
            .AsNoTracking()
            .Where(p => p.DecisionType == type && p.SupersededAtUtc == null);

        // A requested segment matches its own patterns plus tenant-wide (null-segment) fallbacks.
        query = seg is null
            ? query.Where(p => p.Segment == null)
            : query.Where(p => p.Segment == seg || p.Segment == null);

        var patterns = await query.ToListAsync(cancellationToken);

        return patterns
            .OrderByDescending(p => p.Segment != null) // segment-specific ahead of tenant-wide
            .ThenByDescending(p => p.Confidence)
            .ThenByDescending(p => p.ObservationCount)
            .Take(limit)
            .Select(ToView)
            .ToList();
    }

    public async Task<DecisionPatternView> ReinforceAsync(
        ReinforceDecisionPatternRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DecisionType))
        {
            throw new ArgumentException("A decision type is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Statement))
        {
            throw new ArgumentException("A statement is required.", nameof(request));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;
        var type = request.DecisionType.Trim();
        var seg = Normalize(request.Segment);
        var statement = request.Statement.Trim();

        var current = await _dbContext.DecisionPatterns
            .Where(p => p.TenantId == tenantId
                        && p.DecisionType == type
                        && p.Segment == seg
                        && p.SupersededAtUtc == null)
            .OrderByDescending(p => p.LastReinforcedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            var seeded = NewPattern(tenantId, type, seg, statement, request.PayloadJson, contradictionSeed: false, now);
            _dbContext.DecisionPatterns.Add(seeded);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToView(seeded);
        }

        if (request.Contradicts)
        {
            // A reversal is itself signal: supersede the current pattern and start a fresh one.
            current.SupersededAtUtc = now;
            var replacement = NewPattern(tenantId, type, seg, statement, request.PayloadJson, contradictionSeed: true, now);
            _dbContext.DecisionPatterns.Add(replacement);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToView(replacement);
        }

        // Confirming outcome: reinforce in place. Confidence rises asymptotically toward 1.0.
        current.ObservationCount += 1;
        current.Confidence = Math.Min(MaxConfidence, current.Confidence + (1m - current.Confidence) * ReinforceStep);
        current.Statement = statement;
        if (request.PayloadJson is not null)
        {
            current.PayloadJson = request.PayloadJson;
        }

        current.LastReinforcedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToView(current);
    }

    public async Task<bool> SupersedeAsync(Guid patternId, CancellationToken cancellationToken = default)
    {
        var pattern = await _dbContext.DecisionPatterns
            .FirstOrDefaultAsync(p => p.Id == patternId && p.SupersededAtUtc == null, cancellationToken);

        if (pattern is null)
        {
            return false;
        }

        pattern.SupersededAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static DecisionPattern NewPattern(
        Guid tenantId, string type, string? seg, string statement, string? payloadJson, bool contradictionSeed, DateTime now)
        => new()
        {
            TenantId = tenantId,
            DecisionType = type,
            Segment = seg,
            Statement = statement,
            PayloadJson = payloadJson,
            ObservationCount = 1,
            Confidence = contradictionSeed ? ContradictionSeedConfidence : SeedConfidence,
            LastReinforcedAtUtc = now,
        };

    private static string? Normalize(string? segment)
        => string.IsNullOrWhiteSpace(segment) ? null : segment.Trim();

    private static DecisionPatternView ToView(DecisionPattern p)
        => new(p.Id, p.DecisionType, p.Segment, p.Statement, p.PayloadJson, p.ObservationCount, p.Confidence, p.LastReinforcedAtUtc);
}
