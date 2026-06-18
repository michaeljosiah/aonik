using Aonik.Commerce.Agents;
using Aonik.SharedKernel.Abstractions.Agents;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// The commerce agent's tool-approval classification (Spec 042 §13 / Spec 032). Read tools pass
/// through unclassified; cart writes are Low; catalog/price/inventory/checkout writes are Medium;
/// nothing is High (Commerce never captures money).
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
    public void ReadTools_Should_BeUnclassified(string tool)
        => _manifest.Classify(tool).Should().BeNull();

    [Fact]
    public void NoTool_Should_BeHigh_BecauseCommerceNeverCapturesMoney()
    {
        string[] all =
        [
            "commerce_create_cart", "commerce_add_to_cart", "commerce_add_bundle_to_cart",
            "commerce_create_product", "commerce_set_price", "commerce_adjust_inventory", "commerce_checkout",
        ];
        foreach (var tool in all)
        {
            _manifest.Classify(tool)!.Options!.Tier.Should().NotBe(ToolApprovalTier.High);
        }
    }
}
