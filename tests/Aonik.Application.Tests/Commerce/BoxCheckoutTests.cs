using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Checkout;
using Aonik.Commerce.Services.Production;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Commerce;

/// <summary>
/// Spec 068 §9 — box checkout materialisation: one aggregate order item + the kitchen-facing
/// selection landing, delivery as its own item, the A18 drift stop, and the (variant,
/// personalisation) production grouping (A14).
/// </summary>
public class BoxCheckoutTests
{
    private static JsonElement Sel(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static CartAccessContext Token(BoxCartDto dto) => CartAccessContext.ForGuest(dto.CartToken);

    /// <summary>A full 6-box: 4 default jollof + 2 salmon jollof (adjustment +3 each).</summary>
    private static async Task<(BoxTestHarness H, BoxTestHarness.BoxFixture F, BoxCartDto Box)> ArrangeFullBoxAsync(
        BoxTestHarness? harness = null)
    {
        var h = harness ?? new BoxTestHarness();
        var f = await h.BuildAsync("jollof");
        var carts = h.BoxCarts();
        var box = await h.BoxCarts().CreateAsync(new CreateBoxCartCommand(f.BundleProductId, 6));
        await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(f.DishVariants["jollof"], 4, null), Token(box));
        var full = await carts.AddLineAsync(box.Box.CartId, new AddBoxLineCommand(
            f.DishVariants["jollof"], 2, Sel("""{"protein":"salmon"}""")), Token(box));
        return (h, f, full with { CartToken = box.CartToken });
    }

    [Fact]
    public async Task BoxCheckout_Should_MaterialiseOneAggregateItem_AndTheSelectionLanding()
    {
        var (h, f, box) = await ArrangeFullBoxAsync();

        var result = await h.Checkout().CheckoutAsync(
            new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), Token(box));

        // Goods: box 95 + personalisation (2 × 3) = 101; no per-dish price anywhere.
        result.Total.Should().Be(101m);
        h.Payments.LastAmount.Should().Be(101m);

        await using var ordering = h.Ordering();
        var order = await ordering.Orders.Include(o => o.Items).FirstAsync(o => o.Id == result.OrderId);
        order.Items.Should().HaveCount(1, "one OrderItem per priced thing — the box");
        var item = order.Items.Single();
        item.Quantity.Should().Be(1m);
        item.UnitPrice.Should().Be(101m);
        item.ProductId.Should().Be(f.BundleProductId);
        item.DetailsJson.Should().Contain("\"boxPrice\":95").And.Contain("\"personalisation\":6");

        await using var commerce = h.Commerce();
        var selections = await commerce.OrderBundleSelections
            .Where(s => s.OrderId == result.OrderId)
            .ToListAsync();
        selections.Should().HaveCount(2, "one row per BoxDish line");
        selections.Sum(s => s.Quantity).Should().Be(6m);
        var salmon = selections.Single(s => s.PersonalisationSummary!.Contains("Salmon"));
        salmon.PersonalisationAdjustment.Should().Be(3m);
        salmon.PersonalisationJson.Should().Contain("\"protein\":\"salmon\"");
        salmon.PersonalisationEnvelopeJson.Should().Contain("\"breakdown\"", "the §12 envelope is immutable full fidelity");
        selections.Should().OnlyContain(s => s.BundleSlotId == f.SlotId);
    }

    [Fact]
    public async Task DeliveryCharged_Should_MaterialiseItsOwnOrderItem_AndJoinTheTotal()
    {
        // A22 — dormant at 0; when configured the fee is an order item, never absorbed.
        var h = new BoxTestHarness();
        h.Settings["Commerce.Storefront.DeliveryChargedAmount"] = "4.50";
        var (_, f, box) = await ArrangeFullBoxAsync(h);

        var result = await h.Checkout().CheckoutAsync(
            new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), Token(box));

        result.Total.Should().Be(101m + 4.50m, "subtotal − discount + tax + delivery");
        h.Payments.LastAmount.Should().Be(105.50m);

        await using var ordering = h.Ordering();
        var order = await ordering.Orders.Include(o => o.Items).FirstAsync(o => o.Id == result.OrderId);
        order.Items.Should().HaveCount(2);
        var delivery = order.Items.Single(i => i.ItemType == "DeliveryFee");
        delivery.AmountIn.Should().Be(4.50m);
        order.Items.Single(i => i.ItemType != "DeliveryFee").UnitPrice.Should().Be(101m, "the box item carries only the goods total");
    }

    [Fact]
    public async Task StaleCheckout_Should_Stop409WithTheRefreshedBox_AndResubmitSucceed()
    {
        // A18 — an option retired after the last GET; direct checkout must not move money.
        var (h, f, box) = await ArrangeFullBoxAsync();

        var salmonId = await f.Options.ChoiceIdAsync("protein", "salmon");
        var options = CommerceTestHarness.NewOptionService(h.Commerce(), h.TenantId);
        await options.UpdateChoiceAsync(salmonId, new UpdateOptionChoiceCommand("Salmon", IsActive: false));

        var checkout = h.Checkout();
        var stale = () => checkout.CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), Token(box));
        var drift = (await stale.Should().ThrowAsync<BoxCheckoutDriftException>()).Which;

        drift.Refreshed.Changes.Should().Contain(c => c.Reason == "option-retired");
        h.Payments.Calls.Should().Be(0, "nothing is reserved, no order or payment exists");
        await using (var ordering = h.Ordering())
        {
            (await ordering.Orders.CountAsync()).Should().Be(0);
        }

        // The repair persisted (salmon remapped onto the default line) — resubmission proceeds.
        var result = await h.Checkout().CheckoutAsync(
            new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), Token(box));
        result.Total.Should().Be(95m, "the remapped box is all defaults again");
    }

    [Fact]
    public async Task BoxCheckout_Should_ReplayIdempotently()
    {
        var (h, _, box) = await ArrangeFullBoxAsync();
        var checkout = h.Checkout();

        var first = await checkout.CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), Token(box));
        var retry = await checkout.CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), Token(box));

        retry.OrderId.Should().Be(first.OrderId);
        retry.Total.Should().Be(first.Total);
        await using var ordering = h.Ordering();
        (await ordering.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ProductionSheet_Should_GroupByVariantAndPersonalisation()
    {
        // A14 — two Jollof preparations are two demand lines; collapsing them can never be undone.
        var (h, _, box) = await ArrangeFullBoxAsync();
        var checkout = h.Checkout();
        var result = await checkout.CheckoutAsync(new CheckoutCommand(box.Box.CartId, "Stripe", "Card"), Token(box));
        // Draft orders are deliberately not kitchen demand (§9) — payment completion admits them.
        await checkout.ConfirmPaymentAsync(result.OrderId);

        var tenant = new Aonik.TestSupport.Multitenancy.TestTenantProvider(h.TenantId);
        var planning = new ProductionPlanningService(h.Commerce(),
            new Aonik.Ordering.Services.CoreOrderService(h.Ordering(), tenant,
                new CommerceTestHarness.TestClock(),
                new Aonik.TestSupport.Identity.TestCurrentUserProvider()),
            new RecipeService(h.Commerce(), tenant),
            h.Inventory(),
            tenant);

        // Orders are admitted by audit CreatedAt, which is context-stamped with the wall clock —
        // a ±2-day window around now stays inside the 92-day cap and brackets it.
        var sheet = await planning.GetProductionSheetAsync(new(
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(2)));

        sheet.Lines.Should().HaveCount(2, "default and salmon preparations must not collapse");
        var salmon = sheet.Lines.Single(l => l.PersonalisationSummary!.Contains("Salmon"));
        salmon.PortionsDemanded.Should().Be(2m);
        salmon.PersonalisationDisplayJson.Should().NotBeNullOrEmpty("label-snapshotted display rides along");
        sheet.Lines.Single(l => l.PersonalisationSummary == "").PortionsDemanded.Should().Be(4m);
    }
}
