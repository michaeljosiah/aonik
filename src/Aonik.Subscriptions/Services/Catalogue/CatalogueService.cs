using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Contracts.Models;
using Aonik.Subscriptions.Contracts.Services;
using Aonik.Subscriptions.Entities.Catalogue;
using Aonik.Subscriptions.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Subscriptions.Services.Catalogue;

/// <summary>
/// Spec 087 §6 — authoring the catalogue. All business logic lives here; the entities are anemic
/// per ADR-002.
/// </summary>
internal sealed class CatalogueService : ICatalogueService
{
    private readonly SubscriptionsDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public CatalogueService(
        SubscriptionsDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    // ---- Meters ------------------------------------------------------------------------------

    public async Task<MeterResponse> CreateMeterAsync(CreateMeterRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var code = NormaliseCode(request.Code);

        if (!IsKnownMeterKind(request.Kind))
            throw new InvalidStateException($"'{request.Kind}' is not a known meter kind.");

        var exists = await _dbContext.Meters
            .AnyAsync(m => m.TenantId == tenantId && m.Code == code, cancellationToken);

        if (exists)
            throw new InvalidStateException($"A meter with code '{code}' already exists.");

        var meter = new Meter
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            DisplayName = request.DisplayName,
            Kind = request.Kind,
            Unit = request.Unit
        };

        _dbContext.Meters.Add(meter);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapMeter(meter);
    }

    public async Task<MeterResponse> UpdateMeterAsync(Guid meterId, UpdateMeterRequest request, CancellationToken cancellationToken = default)
    {
        var meter = await _dbContext.Meters.FirstOrDefaultAsync(m => m.Id == meterId, cancellationToken)
            ?? throw new NotFoundException($"Meter '{meterId}' was not found.");

        // Kind is intentionally absent from the request — see ICatalogueService.
        meter.DisplayName = request.DisplayName;
        meter.Unit = request.Unit;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapMeter(meter);
    }

    public async Task<MeterResponse?> GetMeterAsync(Guid meterId, CancellationToken cancellationToken = default)
    {
        var meter = await _dbContext.Meters.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == meterId, cancellationToken);

        return meter is null ? null : MapMeter(meter);
    }

    public async Task<IReadOnlyList<MeterResponse>> ListMetersAsync(CancellationToken cancellationToken = default)
    {
        var meters = await _dbContext.Meters.AsNoTracking()
            .OrderBy(m => m.Code)
            .ToListAsync(cancellationToken);

        return meters.Select(MapMeter).ToList();
    }

    // ---- Plans -------------------------------------------------------------------------------

    public async Task<PlanResponse> CreatePlanAsync(CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var code = NormaliseCode(request.Code);

        var exists = await _dbContext.Plans
            .AnyAsync(p => p.TenantId == tenantId && p.Code == code, cancellationToken);

        if (exists)
            throw new InvalidStateException($"A plan with code '{code}' already exists.");

        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Name = request.Name,
            Description = request.Description,
            BillingInterval = request.BillingInterval,
            Status = PlanStatuses.Draft,
            SortOrder = request.SortOrder
        };

        _dbContext.Plans.Add(plan);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapPlan(plan, []);
    }

    public async Task<PlanResponse> UpdatePlanAsync(Guid planId, UpdatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.Plans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken)
            ?? throw new NotFoundException($"Plan '{planId}' was not found.");

        plan.Name = request.Name;
        plan.Description = request.Description;
        plan.SortOrder = request.SortOrder;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetPlanAsync(planId, cancellationToken)
            ?? throw new NotFoundException($"Plan '{planId}' was not found.");
    }

    public Task<PlanResponse?> GetPlanAsync(Guid planId, CancellationToken cancellationToken = default)
        => LoadPlanAsync(p => p.Id == planId, cancellationToken);

    public Task<PlanResponse?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalised = NormaliseCode(code);
        return LoadPlanAsync(p => p.Code == normalised, cancellationToken);
    }

    public async Task<IReadOnlyList<PlanResponse>> ListPlansAsync(bool includeRetired = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Plans.AsNoTracking();

        if (!includeRetired)
            query = query.Where(p => p.Status != PlanStatuses.Retired);

        var plans = await query.OrderBy(p => p.SortOrder).ThenBy(p => p.Code).ToListAsync(cancellationToken);
        var planIds = plans.Select(p => p.Id).ToList();

        var versions = await LoadVersionsAsync(v => planIds.Contains(v.PlanId), cancellationToken);

        return plans
            .Select(p => MapPlan(p, versions.Where(v => v.PlanId == p.Id).ToList()))
            .ToList();
    }

    public async Task<PlanResponse> RetirePlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await _dbContext.Plans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken)
            ?? throw new NotFoundException($"Plan '{planId}' was not found.");

        plan.Status = PlanStatuses.Retired;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetPlanAsync(planId, cancellationToken)
            ?? throw new NotFoundException($"Plan '{planId}' was not found.");
    }

    // ---- Versions ----------------------------------------------------------------------------

    public async Task<PlanVersionResponse> CreateDraftVersionAsync(Guid planId, CreatePlanVersionRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var plan = await _dbContext.Plans.FirstOrDefaultAsync(p => p.Id == planId, cancellationToken)
            ?? throw new NotFoundException($"Plan '{planId}' was not found.");

        var existingDraft = await _dbContext.PlanVersions
            .AnyAsync(v => v.PlanId == planId && v.Status == PlanVersionStatuses.Draft, cancellationToken);

        if (existingDraft)
            throw new InvalidStateException($"Plan '{plan.Code}' already has a draft version. Publish or amend it before starting another.");

        if (request.Price < 0)
            throw new InvalidStateException("Price cannot be negative.");

        var highest = await _dbContext.PlanVersions
            .Where(v => v.PlanId == planId)
            .Select(v => (int?)v.Version)
            .MaxAsync(cancellationToken) ?? 0;

        var version = new PlanVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanId = planId,
            Version = highest + 1,
            Price = request.Price,
            Currency = request.Currency,
            EffectiveFrom = request.EffectiveFrom ?? _clock.UtcNow,
            Status = PlanVersionStatuses.Draft
        };

        _dbContext.PlanVersions.Add(version);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapVersion(version, [], new Dictionary<string, Meter>());
    }

    public async Task<PlanVersionResponse> SetEntitlementsAsync(Guid planVersionId, SetEntitlementsRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var version = await _dbContext.PlanVersions
            .Include(v => v.Entitlements)
            .FirstOrDefaultAsync(v => v.Id == planVersionId, cancellationToken)
            ?? throw new NotFoundException($"Plan version '{planVersionId}' was not found.");

        RequireDraft(version);

        var codes = request.Entitlements.Select(e => NormaliseCode(e.MeterCode)).ToList();

        if (codes.Count != codes.Distinct().Count())
            throw new InvalidStateException("The same meter appears more than once.");

        // The meter table is the authority: an unknown code fails closed rather than being
        // written and discovered later at grant materialisation.
        var meters = await _dbContext.Meters.AsNoTracking()
            .Where(m => m.TenantId == tenantId && codes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, cancellationToken);

        var missing = codes.Where(c => !meters.ContainsKey(c)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException($"Unknown meter code(s): {string.Join(", ", missing)}.");

        foreach (var spec in request.Entitlements)
        {
            var meter = meters[NormaliseCode(spec.MeterCode)];
            ValidateAllowanceForKind(meter, spec);
        }

        _dbContext.PlanEntitlements.RemoveRange(version.Entitlements);

        foreach (var spec in request.Entitlements)
        {
            _dbContext.PlanEntitlements.Add(new PlanEntitlement
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PlanVersionId = version.Id,
                MeterCode = NormaliseCode(spec.MeterCode),
                Allowance = spec.Allowance,
                ResetPolicy = string.IsNullOrWhiteSpace(spec.ResetPolicy) ? ResetPolicies.Period : spec.ResetPolicy
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetVersionAsync(planVersionId, cancellationToken)
            ?? throw new NotFoundException($"Plan version '{planVersionId}' was not found.");
    }

    public async Task<PlanVersionResponse> PublishVersionAsync(Guid planVersionId, CancellationToken cancellationToken = default)
    {
        var version = await _dbContext.PlanVersions
            .Include(v => v.Entitlements)
            .FirstOrDefaultAsync(v => v.Id == planVersionId, cancellationToken)
            ?? throw new NotFoundException($"Plan version '{planVersionId}' was not found.");

        RequireDraft(version);

        if (version.Entitlements.Count == 0)
            throw new InvalidStateException("A version with no entitlements confers nothing and cannot be published.");

        var previouslyPublished = await _dbContext.PlanVersions
            .Where(v => v.PlanId == version.PlanId && v.Status == PlanVersionStatuses.Published)
            .ToListAsync(cancellationToken);

        foreach (var superseded in previouslyPublished)
            superseded.Status = PlanVersionStatuses.Superseded;

        version.Status = PlanVersionStatuses.Published;
        version.PublishedAt = _clock.UtcNow;

        // Publishing the first version is what makes the plan offerable.
        var plan = await _dbContext.Plans.FirstOrDefaultAsync(p => p.Id == version.PlanId, cancellationToken);
        if (plan is not null && plan.Status == PlanStatuses.Draft)
            plan.Status = PlanStatuses.Active;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetVersionAsync(planVersionId, cancellationToken)
            ?? throw new NotFoundException($"Plan version '{planVersionId}' was not found.");
    }

    public async Task<PlanVersionResponse?> GetVersionAsync(Guid planVersionId, CancellationToken cancellationToken = default)
    {
        var versions = await LoadVersionsAsync(v => v.Id == planVersionId, cancellationToken);
        return versions.FirstOrDefault();
    }

    public async Task<PlanVersionResponse?> GetCurrentVersionAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var versions = await LoadVersionsAsync(
            v => v.PlanId == planId && v.Status == PlanVersionStatuses.Published,
            cancellationToken);

        return versions.OrderByDescending(v => v.Version).FirstOrDefault();
    }

    // ---- Internals ---------------------------------------------------------------------------

    private static void RequireDraft(PlanVersion version)
    {
        if (version.Status != PlanVersionStatuses.Draft)
        {
            // A published version is pinned by live subscriptions. Editing it would re-price and
            // reshape every one of them, which is exactly the guarantee versioning exists to give.
            throw new InvalidStateException(
                $"Plan version {version.Version} is '{version.Status}' and can no longer be changed. Create a new version instead.");
        }
    }

    private static void ValidateAllowanceForKind(Meter meter, PlanEntitlementSpec spec)
    {
        if (spec.Allowance < 0)
            throw new InvalidStateException($"Allowance for '{meter.Code}' cannot be negative.");

        switch (meter.Kind)
        {
            case MeterKinds.Flag when spec.Allowance is not (0 or 1):
                throw new InvalidStateException($"'{meter.Code}' is a flag; its allowance must be 0 or 1.");

            case MeterKinds.Ceiling when decimal.Truncate(spec.Allowance) != spec.Allowance:
                throw new InvalidStateException($"'{meter.Code}' is a ceiling; its allowance must be a whole number of slots.");

            case MeterKinds.Counter when !IsKnownResetPolicy(spec.ResetPolicy):
                throw new InvalidStateException($"'{spec.ResetPolicy}' is not a known reset policy.");
        }
    }

    private async Task<PlanResponse?> LoadPlanAsync(
        System.Linq.Expressions.Expression<Func<Plan, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var plan = await _dbContext.Plans.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);
        if (plan is null)
            return null;

        var versions = await LoadVersionsAsync(v => v.PlanId == plan.Id, cancellationToken);
        return MapPlan(plan, versions);
    }

    private async Task<List<PlanVersionResponse>> LoadVersionsAsync(
        System.Linq.Expressions.Expression<Func<PlanVersion, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var versions = await _dbContext.PlanVersions.AsNoTracking()
            .Where(predicate)
            .OrderBy(v => v.PlanId).ThenBy(v => v.Version)
            .ToListAsync(cancellationToken);

        if (versions.Count == 0)
            return [];

        var versionIds = versions.Select(v => v.Id).ToList();

        var entitlements = await _dbContext.PlanEntitlements.AsNoTracking()
            .Where(e => versionIds.Contains(e.PlanVersionId))
            .ToListAsync(cancellationToken);

        // Kind and unit are read from the meter, never stored on the entitlement.
        var codes = entitlements.Select(e => e.MeterCode).Distinct().ToList();
        var meters = await _dbContext.Meters.AsNoTracking()
            .Where(m => codes.Contains(m.Code))
            .ToDictionaryAsync(m => m.Code, cancellationToken);

        return versions
            .Select(v => MapVersion(
                v,
                entitlements.Where(e => e.PlanVersionId == v.Id).ToList(),
                meters))
            .ToList();
    }

    private static PlanResponse MapPlan(Plan plan, IReadOnlyList<PlanVersionResponse> versions)
        => new(plan.Id, plan.Code, plan.Name, plan.Description, plan.BillingInterval, plan.Status, plan.SortOrder, versions);

    private static PlanVersionResponse MapVersion(
        PlanVersion version,
        IReadOnlyList<PlanEntitlement> entitlements,
        IReadOnlyDictionary<string, Meter> meters)
        => new(
            version.Id,
            version.PlanId,
            version.Version,
            version.Price,
            version.Currency,
            version.EffectiveFrom,
            version.Status,
            version.PublishedAt,
            entitlements
                .OrderBy(e => e.MeterCode)
                .Select(e => new PlanEntitlementResponse(
                    e.Id,
                    e.MeterCode,
                    meters.TryGetValue(e.MeterCode, out var meter) ? meter.Kind : string.Empty,
                    meters.TryGetValue(e.MeterCode, out var withUnit) ? withUnit.Unit : null,
                    e.Allowance,
                    e.ResetPolicy))
                .ToList());

    private static MeterResponse MapMeter(Meter meter)
        => new(meter.Id, meter.Code, meter.DisplayName, meter.Kind, meter.Unit);

    private static string NormaliseCode(string code) => code.Trim().ToLowerInvariant();

    private static bool IsKnownMeterKind(string kind)
        => kind is MeterKinds.Counter or MeterKinds.Ceiling or MeterKinds.Flag;

    private static bool IsKnownResetPolicy(string policy)
        => policy is ResetPolicies.Period or ResetPolicies.Never;
}
