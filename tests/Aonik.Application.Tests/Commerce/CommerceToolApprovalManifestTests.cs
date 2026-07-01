using Aonik.Commerce.Agents;
using Aonik.SharedKernel.Abstractions.Agents;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// The commerce agent's tool-approval classification (Spec 042 §13 / Spec 032 / Spec 050 §12 /
/// Spec 051 §11 / Spec 052 §11 / Spec 053 §14). Read tools pass through unclassified; cart writes
/// are Low; catalog/price/inventory/checkout, maker-ops master-data + costing + raw-material
/// stock, and sourcing (supplier / purchase-order placement) writes are Medium; nothing is High
/// (Commerce never captures or pays out money — paying a purchase-order supplier is the deferred
/// Spec 053 high-tier follow-up, deliberately not registered).
/// </summary>
public class CommerceToolApprovalManifestTests
{
    private readonly CommerceToolApprovalManifest _manifest = new();

    [Fact]
    public void Module_Should_BeCommerce()
        => ((IToolApprovalManifest)_manifest).Module.Should().Be("Commerce");

    [Theory]
    [InlineData("commerce_create_cart")]
    [InlineData("commerce_add_to_cart")]
    [InlineData("commerce_add_bundle_to_cart")]
    public void CartWrites_Should_BeLow(string tool)
    {
        var classification = _manifest.Classify(tool);
        classification.Should().NotBeNull();
        classification!.Options!.Tier.Should().Be(ToolApprovalTier.Low);
    }

    [Theory]
    [InlineData("commerce_create_product")]
    [InlineData("commerce_set_price")]
    [InlineData("commerce_adjust_inventory")]
    [InlineData("commerce_checkout")]
    [InlineData("commerce_create_ingredient")]
    [InlineData("commerce_set_recipe")]
    [InlineData("commerce_update_ingredient_cost")]
    [InlineData("commerce_set_ingredient_stock")]
    [InlineData("commerce_set_reorder_point")]
    [InlineData("commerce_create_supplier")]
    [InlineData("commerce_create_purchase_order")]
    [InlineData("commerce_submit_purchase_order")]
    public void DomainWritesAndCheckout_Should_BeMedium(string tool)
    {
        var classification = _manifest.Classify(tool);
        classification.Should().NotBeNull();
        classification!.Options!.Tier.Should().Be(ToolApprovalTier.Medium);
    }

    [Theory]
    [InlineData("commerce_search_products")]
    [InlineData("commerce_get_product")]
    [InlineData("commerce_view_cart")]
    [InlineData("commerce_check_inventory")]
    [InlineData("commerce_list_ingredients")]
    [InlineData("commerce_get_recipe")]
    [InlineData("commerce_explode_recipe")]
    [InlineData("commerce_get_product_cost")]
    [InlineData("commerce_check_ingredient_stock")]
    [InlineData("commerce_list_low_stock")]
    [InlineData("commerce_list_suppliers")]
    public void ReadTools_Should_BeUnclassified(string tool)
        => _manifest.Classify(tool).Should().BeNull();

    [Fact]
    public void NoTool_Should_BeHigh_BecauseCommerceNeverCapturesMoney()
    {
        string[] all =
        [
            "commerce_create_cart", "commerce_add_to_cart", "commerce_add_bundle_to_cart",
            "commerce_create_product", "commerce_set_price", "commerce_adjust_inventory", "commerce_checkout",
            "commerce_create_ingredient", "commerce_set_recipe", "commerce_update_ingredient_cost",
            "commerce_set_ingredient_stock", "commerce_set_reorder_point",
            "commerce_create_supplier", "commerce_create_purchase_order", "commerce_submit_purchase_order",
        ];
        foreach (var tool in all)
        {
            _manifest.Classify(tool)!.Options!.Tier.Should().NotBe(ToolApprovalTier.High);
        }
    }
}
