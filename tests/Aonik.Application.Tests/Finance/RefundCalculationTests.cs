using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Payments;

using FluentAssertions;

namespace Aonik.Application.Tests.Finance;

public sealed class RefundCalculationTests
{
    [Fact]
    public void Components_Should_NormalizeFourDecimalDiscountSharesToConservedPennies()
    {
        // Original £10/£20 goods carry a £1 coupon allocated as £0.3333/£0.6667.
        var source = Source(29m, 0m, Line("first", 9.6667m), Line("second", 19.3333m));

        var components = RefundCalculation.Components(source, []);
        var full = RefundCalculation.Calculate(source, Full("first", "second"), []);

        components.Select(x => x.OriginalAmount).Should().Equal(9.67m, 19.33m);
        full.Components.Select(x => x.Amount).Should().Equal(9.67m, 19.33m);
        full.Preview.Total.Should().Be(29m);
        full.Preview.CashAmount.Should().Be(29m);
    }

    [Fact]
    public void PartialRefunds_Should_FloorCumulativeGiftShareAndConserveBothOriginalRails()
    {
        var source = Source(0.06m, 0.03m,
            Line("first", 0.03m, gift: 0.015m), Line("second", 0.03m, gift: 0.015m));
        var firstDraft = Partial("first", 0.01m);
        var first = RefundCalculation.Calculate(source, firstDraft, []);
        var prior = new List<RefundSnapshot> { Snapshot(source, firstDraft, first) };
        var secondDraft = Full("first");
        var second = RefundCalculation.Calculate(source, secondDraft, prior);
        prior.Add(Snapshot(source, secondDraft, second));
        var last = RefundCalculation.Calculate(source, Full("second"), prior);

        first.Preview.GiftAmount.Should().Be(0m);
        first.Preview.CashAmount.Should().Be(0.01m);
        second.Preview.GiftAmount.Should().Be(0.01m);
        second.Preview.CashAmount.Should().Be(0.01m);
        last.Preview.GiftAmount.Should().Be(0.02m);
        last.Preview.CashAmount.Should().Be(0.01m);
        (first.Preview.GiftAmount + second.Preview.GiftAmount + last.Preview.GiftAmount).Should().Be(0.03m);
        (first.Preview.CashAmount + second.Preview.CashAmount + last.Preview.CashAmount).Should().Be(0.03m);
        last.Preview.Warnings.Should().Contain(x => x.Contains("original expiry"));
    }

    [Fact]
    public void PartialRefunds_Should_UseCumulativeIntegerPointsAndReturnExactFinalRemainder()
    {
        var source = Source(3m, 0m, Line("food", 3m, earned: 5, redeemed: 7));
        var draft = Partial("food", 1m);
        var first = RefundCalculation.Calculate(source, draft, []);
        var prior = new List<RefundSnapshot> { Snapshot(source, draft, first) };
        var second = RefundCalculation.Calculate(source, draft, prior);
        prior.Add(Snapshot(source, draft, second));
        var last = RefundCalculation.Calculate(source, Full("food"), prior);

        first.Preview.EarnedPointsToReverse.Should().Be(1);
        second.Preview.EarnedPointsToReverse.Should().Be(2);
        last.Preview.EarnedPointsToReverse.Should().Be(2);
        first.Preview.RedeemedPointsToRestore.Should().Be(2);
        second.Preview.RedeemedPointsToRestore.Should().Be(2);
        last.Preview.RedeemedPointsToRestore.Should().Be(3);
        prior.Add(Snapshot(source, Full("food"), last));
        RefundCalculation.Components(source, prior).Single().CanRefund.Should().BeFalse();
    }

    [Fact]
    public void FullRemaining_Should_RestorePointsOnZeroMoneyLineExactlyOnce()
    {
        var source = Source(0m, 0m, Line("points-only", 0m, redeemed: 100));
        var draft = Full("points-only");

        var result = RefundCalculation.Calculate(source, draft, []);

        result.Preview.Total.Should().Be(0m);
        result.Preview.CashAmount.Should().Be(0m);
        result.Preview.GiftAmount.Should().Be(0m);
        result.Preview.RedeemedPointsToRestore.Should().Be(100);
        result.Components.Single().CompletesComponent.Should().BeTrue();
        var replay = () => RefundCalculation.Calculate(source, draft, [Snapshot(source, draft, result)]);
        replay.Should().Throw<InvalidStateException>();
    }

    [Fact]
    public void FeeAndTaxSelections_Should_ReturnFundingWithoutInventingLoyalty()
    {
        var source = Source(18m, 0m,
            Line("food", 10m, earned: 20, redeemed: 25),
            Line("delivery", 5m) with { Kind = "DeliveryFee" },
            Line("tax", 3m) with { Kind = "Tax", OrderItemId = null });

        var result = RefundCalculation.Calculate(source, Full("delivery", "tax"), []);

        result.Preview.CashAmount.Should().Be(8m);
        result.Preview.EarnedPointsToReverse.Should().Be(0);
        result.Preview.RedeemedPointsToRestore.Should().Be(0);
        result.Components.Should().NotContain(x => x.ComponentId == "food");
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("0")]
    [InlineData("0.001")]
    [InlineData("1.01")]
    public void Calculate_Should_RejectInvalidOrOverBudgetAmount(string amount)
    {
        var source = Source(1m, 0m, Line("food", 1m));
        var value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        var act = () => RefundCalculation.Calculate(source, Partial("food", value), []);

        act.Should().Throw<InvalidStateException>();
    }

    [Fact]
    public void Normalize_Should_RejectDuplicateSelectionsAndAmbiguousFullRemainingAmount()
    {
        var duplicate = () => RefundCalculation.Normalize(new("Reason", [new("food", 1m), new("food", 1m)]));
        var ambiguous = () => RefundCalculation.Normalize(new("Reason", [new("food", 1m, true)]));

        duplicate.Should().Throw<InvalidStateException>();
        ambiguous.Should().Throw<InvalidStateException>();
    }

    [Fact]
    public void Calculate_Should_RejectMissingComponentsAndPreviouslySpentBudget()
    {
        var source = Source(2m, 0m, Line("food", 2m));
        var draft = Partial("food", 1.50m);
        var first = RefundCalculation.Calculate(source, draft, []);
        var prior = new[] { Snapshot(source, draft, first) };

        var overBudget = () => RefundCalculation.Calculate(source, Partial("food", 0.51m), prior);
        var unknown = () => RefundCalculation.Calculate(source, Full("not-original"), []);
        overBudget.Should().Throw<InvalidStateException>();
        unknown.Should().Throw<InvalidStateException>();
        RefundCalculation.Components(source, prior).Single().RemainingAmount.Should().Be(0.50m);
    }

    [Fact]
    public void Components_Should_FailClosedOnInconsistentOriginalOrPriorFacts()
    {
        var source = Source(2m, 0m, Line("food", 2m, earned: 2));
        var result = RefundCalculation.Calculate(source, Partial("food", 1m), []);
        var snapshot = Snapshot(source, Partial("food", 1m), result);
        var badPrior = snapshot with { Components = [result.Components[0] with { Amount = 3m }] };
        var badEarned = snapshot with { Components = [result.Components[0] with { EarnedPointsToReverse = 3 }] };

        var negative = () => RefundCalculation.Components(source with { Components = [Line("food", -2m)] }, []);
        var rails = () => RefundCalculation.Components(source with { GiftAmount = 1m }, []);
        var overspent = () => RefundCalculation.Components(source, [badPrior]);
        var overEarned = () => RefundCalculation.Components(source, [badEarned]);
        negative.Should().Throw<InvalidStateException>();
        rails.Should().Throw<InvalidStateException>();
        overspent.Should().Throw<InvalidStateException>();
        overEarned.Should().Throw<InvalidStateException>();
    }

    [Fact]
    public void PreviewVersion_Should_BindOriginalFactsAndSuccessfulRefundHistory()
    {
        var source = Source(3m, 0m, Line("first", 1m), Line("second", 2m));
        var draft = Partial("first", 0.50m);
        var original = RefundCalculation.Calculate(source, draft, []);
        var repeated = RefundCalculation.Calculate(source, draft with { Reason = "  Customer request  " }, []);
        var changedSource = source with { Components = [source.Components[0] with { Amount = 1.5m }, source.Components[1] with { Amount = 1.5m }] };
        var previousDraft = Partial("second", 0.50m);
        var previous = RefundCalculation.Calculate(source, previousDraft, []);

        repeated.Preview.Version.Should().Be(original.Preview.Version);
        RefundCalculation.Calculate(changedSource, draft, []).Preview.Version.Should().NotBe(original.Preview.Version);
        RefundCalculation.Calculate(source, draft, [Snapshot(source, previousDraft, previous)])
            .Preview.Version.Should().NotBe(original.Preview.Version);
    }

    private static CheckoutRefundComponent Line(string id, decimal amount, decimal gift = 0m, long earned = 0, long redeemed = 0)
        => new(id, Guid.NewGuid(), "ProductPurchase", id, amount, gift, earned, redeemed);

    private static CheckoutRefundSource Source(decimal total, decimal gift, params CheckoutRefundComponent[] lines)
        => new(Guid.NewGuid(), Guid.NewGuid(), null, "GBP", total, gift, total - gift, lines);

    private static RefundDraft Partial(string id, decimal amount) => new("Customer request", [new(id, amount)]);
    private static RefundDraft Full(params string[] ids) => new("Customer request", ids.Select(x => new RefundSelection(x, FullRemaining: true)).ToArray());

    private static RefundSnapshot Snapshot(CheckoutRefundSource source, RefundDraft draft, RefundCalculationResult result)
        => new(source, new(Guid.NewGuid(), draft.Reason, draft.Selections, result.Preview.Version), null,
            result.Components, result.Preview.CashAmount, result.Preview.GiftAmount, null, null, null, null);
}
