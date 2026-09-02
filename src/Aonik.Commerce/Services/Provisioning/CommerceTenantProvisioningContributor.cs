using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Provisioning;

/// <summary>
/// Seeds Commerce defaults when the Commerce module is enabled for a tenant. The gate is the tenant's
/// resolved module set (Spec 097 §12.4): the provisioner skips this contributor when
/// <see cref="ModuleIds.Commerce"/> is off, so there is no <c>businessType == "..."</c> branch here
/// (ADR-013) and no manifest lookup either — the pack's <c>modules[]</c> became TenantModule rows before
/// the contributor loop ran. The one Commerce-entity-shaped default that cannot be pure manifest
/// reference-data is a starter <see cref="ProductCategory"/> taxonomy; units and other lookups ride the
/// pack's referenceData (applied generically by the ConfigPackApplier).
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

    public CommerceTenantProvisioningContributor(CommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string ModuleName => ModuleIds.Commerce;

    public async Task<TenantProvisioningContribution> ContributeProvisioningAsync(
        TenantProvisioningContext context, CancellationToken cancellationToken = default)
    {
        var actions = new List<string>();

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
