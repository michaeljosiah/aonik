using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.IntegrationTests.Support;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Database.Tests.Support;

/// <summary>
/// Builds Commerce contexts and the Spec 066 option service over the LocalDB
/// database with the exact provider shape CommerceModule registers in
/// production: SQL Server with <c>EnableRetryOnFailure</c>. The SQL Server
/// twin of the InMemory <c>CommerceTestHarness</c> in Aonik.Application.Tests.
/// </summary>
internal static class CommerceSqlServerHarness
{
    public static CommerceDbContext CreateContext(SqlLocalDbFixture db, Guid tenantId)
        => new(db.CreateOptions<CommerceDbContext>(), new TestTenantProvider(tenantId), new TestCurrentUserProvider());

    public static ProductOptionService CreateOptionService(CommerceDbContext context, Guid tenantId)
        => new(context, new TestTenantProvider(tenantId), new ProductContentReviewFlagger(context), NullLogger<ProductOptionService>.Instance);

    /// <summary>
    /// Seeds the smallest Spec 066 shape the contention and default-move paths
    /// need: one group ("portion") whose recommended default is "light", a second
    /// choice "full" to move the default to, and one active product to narrow.
    /// </summary>
    public static async Task<(Guid GroupId, Guid ProductId)> SeedPortionGroupAndProductAsync(
        SqlLocalDbFixture db, Guid tenantId)
    {
        await using var context = CreateContext(db, tenantId);
        var service = CreateOptionService(context, tenantId);

        var group = await service.CreateGroupAsync(new CreateOptionGroupCommand("portion", "Portion"));
        await service.AddChoiceAsync(group.Id, new AddOptionChoiceCommand("light", "Light table", IsRecommendedDefault: true, SortOrder: 0));
        await service.AddChoiceAsync(group.Id, new AddOptionChoiceCommand("full", "Full table", Price: 10m, SortOrder: 1));

        var product = new Product
        {
            TenantId = tenantId,
            Slug = "jollof",
            Name = "Jollof Rice",
            Kind = ProductKinds.Simple,
            Status = ProductStatuses.Active,
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();

        return (group.Id, product.Id);
    }
}
