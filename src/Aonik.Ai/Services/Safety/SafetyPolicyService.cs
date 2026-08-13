using System.Text.Json;
using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Writes safety thresholds (Spec 096 §15). Policy is <strong>data</strong>, so tuning a band does
/// not require a deployment — which matters because the numbers agreed in S0 will be wrong at first
/// and will need adjusting against real block rates.
///
/// <para>
/// Every write is a <em>new version</em> rather than an edit. An old verdict records the version it
/// was judged under, so mutating a policy in place would silently rewrite the meaning of every
/// decision already taken against it — and the §10.3 evaluation reviews past verdicts.
/// </para>
/// </summary>
public interface ISafetyPolicyService
{
    Task<string> PublishAsync(
        string safetyBand,
        IReadOnlyDictionary<string, double> thresholds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SafetyPolicySnapshot>> ListActiveAsync(CancellationToken cancellationToken = default);
}

internal sealed class SafetyPolicyService : ISafetyPolicyService
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public SafetyPolicyService(AiDbContext dbContext, ITenantProvider tenantProvider, IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<string> PublishAsync(
        string safetyBand,
        IReadOnlyDictionary<string, double> thresholds,
        CancellationToken cancellationToken = default)
    {
        if (!PartySafetyBandNames.All.Contains(safetyBand))
        {
            throw new InvalidOperationException($"Unknown safety band '{safetyBand}'.");
        }

        foreach (var (category, threshold) in thresholds)
        {
            if (!SafetyCategories.All.Contains(category))
            {
                // A typo in a category name would otherwise create a threshold that never matches,
                // leaving the real category on the unknown-category default and looking configured.
                throw new InvalidOperationException($"Unknown safety category '{category}'.");
            }

            if (threshold is < 0 or > 1)
            {
                throw new InvalidOperationException(
                    $"Threshold for '{category}' must be between 0 and 1.");
            }
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var current = await _dbContext.SafetyPolicies
            .Where(p => p.TenantId == tenantId && p.SafetyBand == safetyBand && p.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var policy in current)
        {
            // Deactivated, not deleted. An old verdict names its version, so the row has to survive
            // for that verdict to remain explicable.
            policy.IsActive = false;
        }

        var version = $"{now:yyyyMMddHHmmss}-{safetyBand}";

        _dbContext.SafetyPolicies.Add(new SafetyPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Version = version,
            SafetyBand = safetyBand,
            ThresholdsJson = JsonSerializer.Serialize(thresholds),
            IsActive = true,
            EffectiveFrom = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return version;
    }

    public async Task<IReadOnlyList<SafetyPolicySnapshot>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var policies = await _dbContext.SafetyPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .ToListAsync(cancellationToken);

        return
        [
            .. policies.Select(p => new SafetyPolicySnapshot(
                p.Version,
                p.SafetyBand,
                JsonSerializer.Deserialize<Dictionary<string, double>>(p.ThresholdsJson)
                    ?? new Dictionary<string, double>()))
        ];
    }
}

/// <summary>
/// Band names as the Ai module knows them. Duplicated from <c>PartySafetyBands</c> deliberately:
/// Ai does not reference Platform, and a shared string here would be a dependency for four constants.
/// The architecture test asserts the two lists stay identical.
/// </summary>
public static class PartySafetyBandNames
{
    public const string Under6 = "under-6";
    public const string Age6To9 = "6-9";
    public const string Age10To12 = "10-12";
    public const string Age13ToMajority = "13-to-majority";
    public const string Adult = "adult";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Under6, Age6To9, Age10To12, Age13ToMajority, Adult
    };
}
