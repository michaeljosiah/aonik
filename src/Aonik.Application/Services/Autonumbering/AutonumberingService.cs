using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Aonik.Application.Abstractions.Autonumbering;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Autonumbering;
using Aonik.Domain.Autonumbering.Entities;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Application.Services.Autonumbering;

public class AutonumberingService : IAutonumberingService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public AutonumberingService(
        IAonikDbContext dbContext,
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

        tenantId ??= ResolveTenantId();

        var profile = await _dbContext.AutonumberProfiles
            .FirstOrDefaultAsync(
                candidate => candidate.EntityType == normalizedEntityType
                    && candidate.TenantId == tenantId,
                cancellationToken);

        if (profile == null)
        {
            profile = new AutonumberProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
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

        var profile = await _dbContext.AutonumberProfiles
            .FirstOrDefaultAsync(
                candidate => candidate.EntityType == normalizedEntityType
                    && candidate.TenantId == tenantId,
                cancellationToken);

        if (profile == null)
        {
            throw new InvalidOperationException($"Autonumber profile not found for '{normalizedEntityType}'.");
        }

        if (!profile.IsActive)
        {
            throw new InvalidOperationException($"Autonumber profile '{normalizedEntityType}' is inactive.");
        }

        if (profile.Strategy != AutonumberStrategy.Sequential)
        {
            throw new InvalidOperationException("Only sequential autonumbering is supported at this time.");
        }

        var now = _clock.UtcNow;
        if (ShouldReset(profile, now))
        {
            profile.LastIssuedValue = profile.MinValue - 1;
        }

        var nextValue = profile.LastIssuedValue + 1;
        if (nextValue > profile.MaxValue)
        {
            throw new InvalidOperationException($"Autonumber range exhausted for '{normalizedEntityType}'.");
        }

        profile.LastIssuedValue = nextValue;
        profile.LastIssuedAt = now;

        var prefix = ApplyTokens(profile.PrefixTemplate, now);
        var suffix = ApplyTokens(profile.SuffixTemplate, now);
        var padded = profile.PaddingLength > 0
            ? nextValue.ToString($"D{profile.PaddingLength}")
            : nextValue.ToString();

        var reference = $"{prefix}{padded}{suffix}";

        await _dbContext.SaveChangesAsync(cancellationToken);

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
