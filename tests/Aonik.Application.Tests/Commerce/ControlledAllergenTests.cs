using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

public class ControlledAllergenTests
{
    [Fact]
    public async Task LegacyText_Should_RemainAdminOnly_UntilTheControlledListIsReviewed()
    {
        var (content, productId, _) = await ArrangeAsync();
        await WriteBlockAsync(content, productId, new("Standard", Ingredients: "Rice",
            Allergens: "None", PrecautionaryStatement: "May contain milk."));

        var admin = await content.GetAdminAsync(productId);
        var resolved = await content.ResolveAsync(productId, null);

        admin.Block!.Allergens.Should().Be("None", "operators need the legacy text as a review reference");
        admin.Block.AllergensPresent.Should().BeNull();
        resolved!.Ingredients.Should().Be("Rice");
        resolved.Allergens.Should().BeNull("free text must not assert that this dish has no allergens");
        resolved.AllergensPresent.Should().BeNull();
        resolved.PrecautionaryStatement.Should().BeNull();
        resolved.DeclarationsWithheld.Should().BeTrue();
    }

    [Fact]
    public async Task ReviewedEmptyList_Should_BeDistinctFromUnauthored_WithoutMakingAFreeFromClaim()
    {
        var (content, productId, _) = await ArrangeAsync();
        await WriteBlockAsync(content, productId, new("Standard", Ingredients: "Rice",
            AllergensPresent: [], PrecautionaryStatement: "  May contain milk.  "));

        var admin = await content.GetAdminAsync(productId);
        var resolved = await content.ResolveAsync(productId, null);

        admin.Block!.AllergensPresent.Should().NotBeNull().And.BeEmpty();
        resolved!.AllergensPresent.Should().NotBeNull().And.BeEmpty();
        resolved.Allergens.Should().Be("None of the 14 regulated allergens declared");
        resolved.PrecautionaryStatement.Should().Be("May contain milk.");
        resolved.DeclarationsWithheld.Should().BeFalse();
    }

    [Fact]
    public async Task ControlledList_Should_NormalizeDuplicates_AndIgnoreContradictoryLegacyText()
    {
        var (content, productId, ctx) = await ArrangeAsync();
        await WriteBlockAsync(content, productId, new("Standard", Ingredients: "Milk, celery",
            Allergens: "None", AllergensPresent: [RegulatedAllergen.Milk, RegulatedAllergen.Celery, RegulatedAllergen.Milk]));

        var admin = await content.GetAdminAsync(productId);
        var resolved = await content.ResolveAsync(productId, null);

        admin.Block!.AllergensPresent.Should().Equal(RegulatedAllergen.Celery, RegulatedAllergen.Milk);
        resolved!.AllergensPresent.Should().Equal(RegulatedAllergen.Celery, RegulatedAllergen.Milk);
        resolved.Allergens.Should().Be("Celery, Milk");
        ctx.ProductContents.Single(c => c.ProductId == productId).AllergensPresentJson.Should().Be("[\"Celery\",\"Milk\"]");
    }

    [Fact]
    public async Task UnknownAllergens_Should_BeRejected_ForBlocksAndVariants()
    {
        var (content, productId, _) = await ArrangeAsync();
        await WriteBlockAsync(content, productId, Block());

        var blockWrite = () => WriteBlockAsync(content, productId, Block() with { AllergensPresent = [(RegulatedAllergen)999] });
        var variantWrite = () => AddVariantAsync(content, productId,
            new("""{"protein":"salmon"}""", "Salmon", AllergensPresent: [(RegulatedAllergen)999]));

        await blockWrite.Should().ThrowAsync<StorefrontValidationException>();
        await variantWrite.Should().ThrowAsync<StorefrontValidationException>();
        (await content.ResolveAsync(productId, null))!.AllergensPresent.Should().Equal(RegulatedAllergen.Crustaceans);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedVariantEdit_Should_NotLeakTrackedChangesIntoALaterSave(bool invalidPrecaution)
    {
        var (content, productId, _) = await ArrangeAsync();
        await WriteBlockAsync(content, productId, Block());
        var variant = await AddVariantAsync(content, productId, new("""{"protein":"salmon"}""", "Salmon",
            Ingredients: "Salmon", AllergensPresent: [RegulatedAllergen.Fish]));

        var rejectedEdit = () => content.UpdateVariantAsync(variant.Id, new("""{"protein":"salmon"}""", "Rejected edit",
            Ingredients: "Rice, milk", AllergensPresent: invalidPrecaution ? [RegulatedAllergen.Milk] : [(RegulatedAllergen)999],
            PrecautionaryStatement: invalidPrecaution ? new string('x', 2001) : null), variant.SelectionJson);
        await rejectedEdit.Should().ThrowAsync<StorefrontValidationException>();
        await WriteBlockAsync(content, productId, Block());

        var resolved = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));
        resolved!.ServingLabel.Should().Be("Salmon");
        resolved.Ingredients.Should().Be("Salmon");
        resolved.AllergensPresent.Should().Equal(RegulatedAllergen.Fish);
    }

    [Theory]
    [InlineData("{corrupt")]
    [InlineData("[\"Unknown\"]")]
    [InlineData("[999]")]
    [InlineData("[6]")]
    [InlineData("[\"6\"]")]
    [InlineData("[\"Milk, Fish\"]")]
    [InlineData("null")]
    public async Task InvalidStoredList_Should_WithholdControlledDeclarations_ForBlocksAndVariants(string storedJson)
    {
        var (content, productId, ctx) = await ArrangeAsync();
        await WriteBlockAsync(content, productId, Block());
        var variant = await AddVariantAsync(content, productId, new("""{"protein":"salmon"}""", "Salmon",
            Ingredients: "Salmon", AllergensPresent: [RegulatedAllergen.Fish], PrecautionaryStatement: "May contain milk."));
        ctx.ProductContents.Single(c => c.ProductId == productId).AllergensPresentJson = storedJson;
        ctx.ProductContentVariants.Single(v => v.Id == variant.Id).AllergensPresentJson = storedJson;
        await ctx.SaveChangesAsync();

        var admin = await content.GetAdminAsync(productId);
        admin.Block!.AllergensPresent.Should().BeNull();
        admin.Variants.Single().AllergensPresent.Should().BeNull();
        foreach (var selection in new JsonElement?[] { null, Selection("""{"protein":"salmon"}""") })
        {
            var resolved = await content.ResolveAsync(productId, selection);
            resolved!.AllergensPresent.Should().BeNull();
            resolved.Allergens.Should().BeNull();
            resolved.PrecautionaryStatement.Should().BeNull();
            resolved.DeclarationsWithheld.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Variant_Should_KeepItsOwnAllergensAndPrecaution_AfterTheDefaultChanges()
    {
        var (content, productId, _) = await ArrangeAsync();
        await WriteBlockAsync(content, productId, Block());
        await AddVariantAsync(content, productId, new("""{"protein":"salmon"}""", "Salmon",
            Ingredients: "Salmon", AllergensPresent: [RegulatedAllergen.Fish], PrecautionaryStatement: "May contain sesame."));
        await WriteBlockAsync(content, productId, Block() with
        {
            AllergensPresent = [RegulatedAllergen.Celery],
            PrecautionaryStatement = "May contain mustard.",
        });

        var resolved = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));

        resolved!.AllergensPresent.Should().Equal(RegulatedAllergen.Fish);
        resolved.PrecautionaryStatement.Should().Be("May contain sesame.");
        resolved.DeclarationsWithheld.Should().BeFalse();
    }

    [Fact]
    public async Task UnreviewedVariant_Should_NotInheritTheDefaultsControlledDeclaration()
    {
        var (content, productId, _) = await ArrangeAsync();
        await WriteBlockAsync(content, productId, Block());
        await AddVariantAsync(content, productId, new("""{"protein":"salmon"}""", "Salmon", Ingredients: "Salmon"));

        var resolved = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));

        resolved!.AllergensPresent.Should().BeNull();
        resolved.Allergens.Should().BeNull();
        resolved.PrecautionaryStatement.Should().BeNull();
        resolved.DeclarationsWithheld.Should().BeTrue();
    }

    [Fact]
    public async Task DefaultDeclarations_Should_BeWithheld_WhenStaleOrResolvedForAnotherSelection()
    {
        var (content, productId, ctx) = await ArrangeAsync();
        await WriteBlockAsync(content, productId, Block());

        var wrongSelection = await content.ResolveAsync(productId, Selection("""{"protein":"salmon"}"""));
        ctx.ProductContents.Single(c => c.ProductId == productId).RequiresReview = true;
        await ctx.SaveChangesAsync();
        var stale = await content.ResolveAsync(productId, null);

        foreach (var resolved in new[] { wrongSelection, stale })
        {
            resolved!.AllergensPresent.Should().BeNull();
            resolved.Allergens.Should().BeNull();
            resolved.PrecautionaryStatement.Should().BeNull();
            resolved.DeclarationsWithheld.Should().BeTrue();
        }
    }

    [Fact]
    public async Task BlockSignature_Should_ChangeOnControlledOrPrecautionEdits_AndRejectAStaleSave()
    {
        var (content, productId, _) = await ArrangeAsync();
        var original = await WriteBlockAsync(content, productId, Block());
        var changedAllergens = await WriteBlockAsync(content, productId, Block() with { AllergensPresent = [RegulatedAllergen.Milk] });
        var changedPrecaution = await WriteBlockAsync(content, productId, Block() with
        {
            AllergensPresent = [RegulatedAllergen.Milk], PrecautionaryStatement = "May contain sesame.",
        });

        original.BlockSignature.Should().NotBe(changedAllergens.BlockSignature);
        changedAllergens.BlockSignature.Should().NotBe(changedPrecaution.BlockSignature);
        changedPrecaution.ContentVersion.Should().BeGreaterThan(changedAllergens.ContentVersion);
        var staleSave = () => content.UpsertContentAsync(productId, Block(),
            new BlockWritePrecondition(original.DescribesSelectionJson, original.BlockSignature));
        await staleSave.Should().ThrowAsync<StorefrontValidationException>().WithMessage("*V-C10*");
    }

    private static UpsertProductContentCommand Block() => new("Standard", Ingredients: "Rice, prawn stock",
        AllergensPresent: [RegulatedAllergen.Crustaceans], PrecautionaryStatement: "May contain milk.");

    private static async Task<(ProductContentService Content, Guid ProductId, CommerceDbContext Ctx)> ArrangeAsync()
    {
        var (dbOptions, tenantId) = CommerceTestHarness.NewDb();
        var ctx = CommerceTestHarness.CreateContext(dbOptions, tenantId);
        var builder = new OptionCatalogueBuilder(ctx, tenantId);
        await builder.BuildCatalogueAsync();
        var productId = await builder.BuildProductAsync();
        await builder.OfferAllAsync(productId);
        return (CommerceTestHarness.NewContentService(ctx, tenantId), productId, ctx);
    }

    private static async Task<ProductContentDto> WriteBlockAsync(
        IProductContentService content, Guid productId, UpsertProductContentCommand command)
    {
        var admin = await content.GetAdminAsync(productId);
        return await content.UpsertContentAsync(productId, command,
            new BlockWritePrecondition(admin.CurrentDefaultsSelectionJson, admin.Block?.BlockSignature));
    }

    private static async Task<ProductContentVariantDto> AddVariantAsync(
        IProductContentService content, Guid productId, UpsertContentVariantCommand command)
        => await content.AddVariantAsync(productId, command,
            (await content.GetAdminAsync(productId)).CurrentDefaultsSelectionJson);

    private static JsonElement Selection(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
