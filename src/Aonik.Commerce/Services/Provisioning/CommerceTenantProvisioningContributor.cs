using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Packs;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Provisioning;

/// <summary>
/// Seeds Commerce defaults when a tenant's business-type pack enables the Commerce module (Spec 065 §8).
/// It gates on the manifest's declared <c>modules[]</c> — never a <c>businessType == "..."</c> branch
/// (ADR-013 / the config-pack Codex review): a food-commerce tenant enables Commerce, but so could a
/// future type. The one Commerce-entity-shaped default that cannot be pure manifest reference-data is a
/// starter <see cref="ProductCategory"/> taxonomy; units and other lookups ride the pack's referenceData
/// (applied generically by the ConfigPackApplier). This is Commerce's first provisioning contribution.
/// </summary>
internal sealed class CommerceTenantProvisioningContributor : ITenantProvisioningContributor
{
    private static readonly (string Slug, string Name, int SortOrder)[] DefaultCategories =
    {
        ("food", "Food", 1),
        ("beverages", "Beverages", 2),
        ("bakery", "Bakery", 3),
        ("uncategorised", "Uncategorised", 99),
    };

    private readonly CommerceDbContext _dbContext;
    private readonly IConfigPackSource _packSource;

    public CommerceTenantProvisioningContributor(CommerceDbContext dbContext, IConfigPackSource packSource)
    {
        _dbContext = dbContext;
        _packSource = packSource;
    }

    public string ModuleName => "Commerce";

    public async Task<TenantProvisioningContribution> ContributeProvisioningAsync(
        TenantProvisioningContext context, CancellationToken cancellationToken = default)
    {
        var actions = new List<string>();

        // Gate on manifest module-enablement, not the business-type value (Spec 065 §8).
        var manifest = _packSource.Get(context.BusinessType);
        var commerceEnabled = manifest?.Modules.Contains("Commerce", StringComparer.OrdinalIgnoreCase) ?? false;
        if (!commerceEnabled)
        {
            return new TenantProvisioningContribution(actions);
        }

        // Each seed is idempotent INDEPENDENTLY — a tenant provisioned before a newer seed
        // existed (or a retry after one seed committed) must still receive the others.
        var hasCategories = await _dbContext.ProductCategories
            .AnyAsync(c => c.TenantId == context.TenantId, cancellationToken);
        if (hasCategories)
        {
            actions.Add("Commerce categories already exist - skipped");
        }
        else
        {
            var categories = DefaultCategories
                .Select(category => new ProductCategory
                {
                    Id = Guid.NewGuid(),
                    TenantId = context.TenantId,
                    Slug = category.Slug,
                    Name = category.Name,
                    SortOrder = category.SortOrder,
                    CreatedAt = context.Now,
                    CreatedBy = context.UserId,
                })
                .ToList();

            _dbContext.ProductCategories.AddRange(categories);
            await _dbContext.SaveChangesAsync(cancellationToken);
            actions.Add($"Created {categories.Count} default Commerce categories");
        }

        // Spec 069 §10 — seed a PARKED fulfilment calendar (inactive, no delivery days) so the
        // admin screen has a row to edit rather than a create-from-nothing flow. The tenant's
        // real cadence is operator data entered in admin, never pack content. Idempotent.
        var hasCalendar = await _dbContext.FulfilmentCalendars
            .AnyAsync(c => c.TenantId == context.TenantId, cancellationToken);
        if (!hasCalendar)
        {
            var seed = new FulfilmentCalendar
            {
                Id = Guid.NewGuid(),
                TenantId = context.TenantId,
                Timezone = "Europe/London",
                DeliveryDaysJson = "[]",
                CutoffLocalTime = new TimeOnly(12, 0),
                LeadDays = 0,
                IsActive = false,
                CreatedAt = context.Now,
                CreatedBy = context.UserId,
            };
            _dbContext.FulfilmentCalendars.Add(seed);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                actions.Add("Seeded a parked fulfilment calendar (inactive)");
            }
            catch (DbUpdateException)
            {
                // A concurrent provisioning run won the filtered unique (TenantId) index - the
                // seed exists, which is the contract; idempotent under retries.
                _dbContext.Entry(seed).State = EntityState.Detached;
                actions.Add("Fulfilment calendar already seeded concurrently - skipped");
            }
        }

        return new TenantProvisioningContribution(actions);
    }

    // No-op: the health-check signature carries no BusinessType/Modules, so it cannot tell whether
    // Commerce is enabled for a tenant — flagging "missing categories" would false-positive every
    // non-commerce tenant. The honest choice is to add no issue here.
    public Task ContributeHealthCheckAsync(Guid tenantId, List<string> issues, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
