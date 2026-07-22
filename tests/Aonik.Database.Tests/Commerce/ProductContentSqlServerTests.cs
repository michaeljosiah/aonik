using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Database.Tests.Support;
using Aonik.IntegrationTests.Support;

using FluentAssertions;

namespace Aonik.Database.Tests.Commerce;

/// <summary>
/// Spec 067 §9 on the only provider that can assert it: every content write runs a real
/// transaction under the retrying execution strategy (InMemory opens neither), and the
/// cross-row V-C6 invariant is enforced against committed state through the serialized write.
/// </summary>
public class ProductContentSqlServerTests : IClassFixture<SqlLocalDbFixture>
{
    private readonly SqlLocalDbFixture _db;

    public ProductContentSqlServerTests(SqlLocalDbFixture db) => _db = db;

    [SkippableFact]
    public async Task ContentWrites_Should_RunUnderTheRetryingStrategy_OnRealSqlServer()
    {
        Skip.IfNot(_db.IsAvailable, _db.SkipReason ?? "SQL Server LocalDB unavailable.");
        var tenantId = Guid.NewGuid();
        var (groupId, productId) = await CommerceSqlServerHarness.SeedPortionGroupAndProductAsync(_db, tenantId);

        await using var context = CommerceSqlServerHarness.CreateContext(_db, tenantId);
        var options = CommerceSqlServerHarness.CreateOptionService(context, tenantId);
        await options.SetProductOptionGroupsAsync(productId, new SetProductOptionGroupsCommand(
            [new ProductOptionGroupLine("portion")]));

        var content = new Aonik.Commerce.Services.Catalog.ProductContentService(
            context,
            new Aonik.TestSupport.Multitenancy.TestTenantProvider(tenantId),
            new Aonik.Commerce.Services.Catalog.OptionSelectionService(
                context, options, new Aonik.TestSupport.Multitenancy.TestTenantProvider(tenantId)),
            options);

        // The demote/promote-style transaction shape (begin → validate → mutate → bump → commit)
        // must survive EnableRetryOnFailure — the exact class that failed 100% on SQL Server
        // behind a green InMemory suite in Spec 066 round 2.
        var block = await content.UpsertContentAsync(productId, new UpsertProductContentCommand(
            "Standard", Kcal: 500, Ingredients: "Rice", Allergens: "None"));
        block.ContentVersion.Should().BeGreaterThan(0);

        var variant = await content.AddVariantAsync(productId, new UpsertContentVariantCommand(
            """{"portion":"full"}""", "Full", Kcal: 900));
        variant.SelectionJson.Should().Contain("\"portion\":\"full\"");

        // V-C6 against COMMITTED variant state, through the serialized write path.
        var addSugars = () => content.UpsertContentAsync(productId, new UpsertProductContentCommand(
            "Standard", Kcal: 500, SugarsGrams: 4, Ingredients: "Rice", Allergens: "None"));
        (await addSugars.Should().ThrowAsync<Aonik.Commerce.Services.Catalog.StorefrontValidationException>())
            .Which.Message.Should().Contain("V-C6");

        // The rejected write changed nothing — version and figures are intact.
        var resolved = await content.ResolveAsync(productId, null);
        resolved!.Nutrition.SugarsGrams.Should().BeNull();
    }
}
