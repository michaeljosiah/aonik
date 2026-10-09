using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Aonik.Platform.Contracts.Models.Autonumbering;
using Aonik.Platform.Contracts.Services.Autonumbering;
using Aonik.Platform.Entities.Autonumbering;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Services.Autonumbering;

internal class AutonumberingService : IAutonumberingService
{
    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public AutonumberingService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public async Task<AutonumberProfileSnapshot?> GetProfileAsync(
        string entityType,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityType = NormalizeEntityType(entityType);
        tenantId ??= ResolveTenantId();

        var profile = await _dbContext.AutonumberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.EntityType == normalizedEntityType
                    && candidate.TenantId == tenantId,
                cancellationToken);

        return profile == null ? null : Map(profile);
    }

    public async Task<AutonumberProfileSnapshot> UpsertProfileAsync(
        AutonumberProfileUpsert request,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityType = NormalizeEntityType(request.EntityType);

        if (request.PaddingLength <= 0)
        {
            throw new InvalidOperationException("PaddingLength must be greater than zero.");
        }

        if (request.MinValue <= 0 || request.MaxValue <= 0)
        {
            throw new InvalidOperationException("MinValue and MaxValue must be positive.");
        }

        if (request.MinValue > request.MaxValue)
        {
            throw new InvalidOperationException("MinValue cannot exceed MaxValue.");
        }

        var resolvedTenantId = tenantId ?? ResolveTenantId();

        var profile = await _dbContext.AutonumberProfiles
            .FirstOrDefaultAsync(
                candidate => candidate.EntityType == normalizedEntityType
                    && candidate.TenantId == resolvedTenantId,
                cancellationToken);

        if (profile == null)
        {
            profile = new AutonumberProfile
            {
                Id = Guid.NewGuid(),
                TenantId = resolvedTenantId,
                EntityType = normalizedEntityType,
                LastIssuedValue = request.MinValue - 1,
                LastIssuedAt = null
            };

            _dbContext.AutonumberProfiles.Add(profile);
        }

        profile.PrefixTemplate = request.PrefixTemplate?.Trim() ?? string.Empty;
        profile.SuffixTemplate = request.SuffixTemplate?.Trim() ?? string.Empty;
        profile.Strategy = request.Strategy;
        profile.ResetPolicy = request.ResetPolicy;
        profile.PaddingLength = request.PaddingLength;
        profile.MinValue = request.MinValue;
        profile.MaxValue = request.MaxValue;
        profile.IsActive = request.IsActive;

        if (profile.LastIssuedValue < profile.MinValue - 1)
        {
            profile.LastIssuedValue = profile.MinValue - 1;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(profile);
    }

    public async Task<AutonumberGenerateResult> GenerateAsync(
        AutonumberGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityType = NormalizeEntityType(request.EntityType);
        var tenantId = request.TenantId ?? ResolveTenantId();

        for (var attempt = 0; ; attempt++)
        {
            // Reload only this sequence. A prior allocation or a lost native-version race
            // must not let identity resolution hand back stale LastIssuedValue state.
            foreach (var entry in _dbContext.ChangeTracker.Entries<AutonumberProfile>()
                         .Where(entry => entry.Entity.TenantId == tenantId
                             && entry.Entity.EntityType == normalizedEntityType).ToList())
            {
                if (entry.State != EntityState.Unchanged)
                    throw new InvalidOperationException("Save profile changes before allocating a number.");
                entry.State = EntityState.Detached;
            }
            var profile = await _dbContext.AutonumberProfiles.FirstOrDefaultAsync(
                candidate => candidate.EntityType == normalizedEntityType && candidate.TenantId == tenantId,
                cancellationToken)
                ?? throw new InvalidOperationException($"Autonumber profile not found for '{normalizedEntityType}'.");
            var now = _clock.UtcNow;
            var result = NextReference(profile, now);
            profile.LastIssuedValue = result.SequenceValue;
            profile.LastIssuedAt = now;
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < 4
                && ex.Entries.All(entry => ReferenceEquals(entry.Entity, profile)))
            {
                _dbContext.Entry(profile).State = EntityState.Detached;
            }
            catch
            {
                _dbContext.Entry(profile).State = EntityState.Detached;
                throw;
            }
        }
    }

    public async Task<AutonumberGenerateResult> PreviewAsync(
        AutonumberGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityType = NormalizeEntityType(request.EntityType);
        var tenantId = request.TenantId ?? ResolveTenantId();

        var profile = await _dbContext.AutonumberProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.EntityType == normalizedEntityType
                    && candidate.TenantId == tenantId,
                cancellationToken);

        if (profile == null)
        {
            throw new InvalidOperationException($"Autonumber profile not found for '{normalizedEntityType}'.");
        }

        return NextReference(profile, _clock.UtcNow);
    }

    private static AutonumberGenerateResult NextReference(AutonumberProfile profile, DateTime now)
    {
        if (!profile.IsActive)
            throw new InvalidOperationException($"Autonumber profile '{profile.EntityType}' is inactive.");
        if (profile.Strategy != AutonumberStrategy.Sequential)
            throw new InvalidOperationException("Only sequential autonumbering is supported at this time.");
        if (profile.MinValue <= 0 || profile.MaxValue < profile.MinValue || profile.PaddingLength <= 0)
            throw new InvalidOperationException("The autonumber profile has invalid sequence bounds.");

        var lastValue = ShouldReset(profile, now) ? profile.MinValue - 1 : Math.Max(profile.LastIssuedValue, profile.MinValue - 1);
        if (lastValue >= profile.MaxValue)
            throw new InvalidOperationException($"Autonumber range exhausted for '{profile.EntityType}'.");
        var nextValue = lastValue + 1;
        if (string.Equals(profile.EntityType, "Order", StringComparison.OrdinalIgnoreCase))
        {
            var template = profile.PrefixTemplate + profile.SuffixTemplate;
            var hasYear = template.Contains("{YYYY}", StringComparison.OrdinalIgnoreCase)
                || template.Contains("{YY}", StringComparison.OrdinalIgnoreCase);
            var hasMonth = template.Contains("{MM}", StringComparison.OrdinalIgnoreCase);
            if (profile.PaddingLength > 64
                || profile.ResetPolicy == AutonumberResetPolicy.Monthly && (!hasYear || !hasMonth)
                || profile.ResetPolicy == AutonumberResetPolicy.Yearly && !hasYear
                || !Enum.IsDefined(profile.ResetPolicy))
                throw new InvalidOperationException("Order references require a bounded format and date tokens covering every reset period.");
        }
        var prefix = ApplyTokens(profile.PrefixTemplate, now);
        var suffix = ApplyTokens(profile.SuffixTemplate, now);
        var reference = $"{prefix}{nextValue.ToString($"D{profile.PaddingLength}", CultureInfo.InvariantCulture)}{suffix}";
        if (string.Equals(profile.EntityType, "Order", StringComparison.OrdinalIgnoreCase) && reference.Length > 64)
            throw new InvalidOperationException("Order references cannot exceed 64 characters.");
        return new AutonumberGenerateResult(profile.Id, nextValue, reference);
    }

    private Guid ResolveTenantId()
    {
        if (_tenantProvider.TryGetCurrentTenantId(out var tenantId))
        {
            return tenantId;
        }

        throw new InvalidOperationException("Tenant context is required for autonumbering.");
    }

    private static string NormalizeEntityType(string entityType)
    {
        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("Entity type is required.", nameof(entityType));
        }

        return entityType.Trim();
    }

    private static bool ShouldReset(AutonumberProfile profile, DateTime now)
    {
        if (profile.ResetPolicy == AutonumberResetPolicy.None)
        {
            return false;
        }

        if (!profile.LastIssuedAt.HasValue)
        {
            return true;
        }

        var lastIssued = profile.LastIssuedAt.Value;

        return profile.ResetPolicy switch
        {
            AutonumberResetPolicy.Monthly => lastIssued.Year != now.Year || lastIssued.Month != now.Month,
            AutonumberResetPolicy.Yearly => lastIssued.Year != now.Year,
            _ => false
        };
    }

    private static string ApplyTokens(string template, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        return template
            .Replace("{YYYY}", now.ToString("yyyy", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{YY}", now.ToString("yy", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{MM}", now.ToString("MM", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{DD}", now.ToString("dd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }

    private static AutonumberProfileSnapshot Map(AutonumberProfile profile)
    {
        return new AutonumberProfileSnapshot(
            profile.Id,
            profile.TenantId,
            profile.EntityType,
            profile.PrefixTemplate,
            profile.SuffixTemplate,
            profile.Strategy,
            profile.ResetPolicy,
            profile.PaddingLength,
            profile.MinValue,
            profile.MaxValue,
            profile.LastIssuedValue,
            profile.LastIssuedAt,
            profile.IsActive);
    }
}
