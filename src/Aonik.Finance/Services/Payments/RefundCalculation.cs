using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.SharedKernel.Abstractions.Pricing;

namespace Aonik.Finance.Services.Payments;

internal static class RefundCalculation
{
    public static RefundDraft Normalize(RefundDraft draft)
    {
        if (draft is null || string.IsNullOrWhiteSpace(draft.Reason) || draft.Reason.Trim().Length > 500
            || draft.Selections is null || draft.Selections.Count is < 1 or > 500
            || draft.Selections.Any(x => x is null || string.IsNullOrWhiteSpace(x.ComponentId)
                || x.ComponentId.Length > 40 || (x.FullRemaining ? x.Amount is not null
                    : x.Amount is not > 0 || !IsMoney(x.Amount.Value)))
            || draft.Selections.Select(x => x.ComponentId).Distinct(StringComparer.Ordinal).Count() != draft.Selections.Count)
            throw new InvalidStateException("Select original components, whole-penny amounts or full remaining value, and a reason of up to 500 characters.");
        return draft with { Reason = draft.Reason.Trim(), Selections = draft.Selections.OrderBy(x => x.ComponentId, StringComparer.Ordinal).ToArray() };
    }

    public static IReadOnlyList<RefundComponentDto> Components(CheckoutRefundSource source, IReadOnlyList<RefundSnapshot> prior)
    {
        ValidateSource(source);
        var amounts = ProportionalAllocation.Allocate(source.Total, source.Components.Select(x => x.Amount).ToArray(),
            capAtWeights: false, precision: 2);
        return source.Components.Select((component, index) =>
        {
            var previous = prior.SelectMany(x => x.Components).Where(x => x.ComponentId == component.ComponentId).ToArray();
            var remaining = amounts[index] - previous.Sum(x => x.Amount);
            if (remaining < 0 || previous.Sum(x => x.EarnedPointsToReverse) > component.EarnedPoints
                || previous.Sum(x => x.RedeemedPointsToRestore) > component.RedeemedPoints)
                throw new InvalidStateException("Previous refunds exceed the original component allocation.");
            var canRefund = !previous.Any(x => x.CompletesComponent) && (remaining > 0 || component.EarnedPoints > 0
                || component.RedeemedPoints > 0 || component.GiftFundedValue > 0);
            return new RefundComponentDto(component.ComponentId, component.Kind, component.Label, amounts[index], remaining, canRefund);
        }).ToArray();
    }

    public static RefundCalculationResult Calculate(CheckoutRefundSource source, RefundDraft draft, IReadOnlyList<RefundSnapshot> prior)
    {
        draft = Normalize(draft);
        var available = Components(source, prior);
        var result = new List<RefundAllocation>();
        foreach (var selection in draft.Selections)
        {
            var component = available.SingleOrDefault(x => x.ComponentId == selection.ComponentId);
            if (component is null || !component.CanRefund)
                throw new InvalidStateException("A selected component has no refundable value.");
            var original = source.Components.Single(x => x.ComponentId == selection.ComponentId);
            var amount = selection.FullRemaining ? component.RemainingAmount : selection.Amount!.Value;
            if (amount > component.RemainingAmount)
                throw new InvalidStateException("The refund exceeds a component's remaining value.");
            var complete = amount == component.RemainingAmount;
            var previous = prior.SelectMany(x => x.Components).Where(x => x.ComponentId == selection.ComponentId).ToArray();
            var fraction = complete ? 1m : (previous.Sum(x => x.Amount) + amount) / component.OriginalAmount;
            var giftWeight = original.GiftFundedValue * fraction - previous.Sum(x => x.GiftWeight);
            var earn = checked((long)Math.Floor(original.EarnedPoints * fraction) - previous.Sum(x => x.EarnedPointsToReverse));
            var restore = checked((long)Math.Floor(original.RedeemedPoints * fraction) - previous.Sum(x => x.RedeemedPointsToRestore));
            if (giftWeight < 0 || earn < 0 || restore < 0) throw new InvalidStateException("The cumulative refund allocation is invalid.");
            result.Add(new(selection.ComponentId, original.OrderItemId, amount, complete, giftWeight, earn, restore));
        }
        var total = result.Sum(x => x.Amount);
        // Cumulative floors preserve pennies across partial returns, including a final remainder.
        var gift = Math.Floor((prior.Sum(x => x.Components.Sum(c => c.GiftWeight)) + result.Sum(x => x.GiftWeight)) * 100m) / 100m
            - prior.Sum(x => x.GiftAmount);
        var cash = total - gift;
        if (gift < 0 || cash < 0 || gift + prior.Sum(x => x.GiftAmount) > source.GiftAmount
            || cash + prior.Sum(x => x.CashAmount) > source.CardAmount)
            throw new InvalidStateException("This selection cannot preserve the original payment split; select the remaining components together.");
        if (total == 0 && !result.Any(x => x.EarnedPointsToReverse > 0 || x.RedeemedPointsToRestore > 0))
            throw new InvalidStateException("The refund has no money or points consequence.");
        var version = RefundSnapshot.Fingerprint(new { source, prior = prior.Select(x => new { x.Request.RefundId, x.Components, x.CashAmount, x.GiftAmount }), draft, result });
        var warnings = new List<string> { "Refunds do not cancel cooking or delivery." };
        if (source.InvoiceId is not null) warnings.Add("Create any required invoice credit note through the manual accounting process; this action returns funding only.");
        if (gift > 0) warnings.Add("Gift-card value returns to the original card with its original expiry date.");
        return new(new(source.Currency, version, draft.Selections, total, cash, gift,
            result.Sum(x => x.RedeemedPointsToRestore), result.Sum(x => x.EarnedPointsToReverse), warnings), result);
    }

    public static bool IsMoney(decimal value) => value == Math.Round(value, 2);

    private static void ValidateSource(CheckoutRefundSource source)
    {
        if (source.Currency != "GBP" || source.Total < 0 || !IsMoney(source.Total)
            || source.GiftAmount < 0 || source.CardAmount < 0 || !IsMoney(source.GiftAmount) || !IsMoney(source.CardAmount)
            || source.Total != source.GiftAmount + source.CardAmount || source.Components.Count is < 1 or > 500
            || source.Components.Select(x => x.ComponentId).Distinct().Count() != source.Components.Count
            || source.Components.Any(x => x.Amount < 0 || x.GiftFundedValue < 0 || x.GiftFundedValue > x.Amount
                || x.EarnedPoints < 0 || x.RedeemedPoints < 0)
            || source.Components.Sum(x => x.Amount) != source.Total || source.Components.Sum(x => x.GiftFundedValue) != source.GiftAmount)
            throw new InvalidStateException("The original paid GBP breakdown is unavailable.");
    }
}

internal sealed record RefundCalculationResult(RefundPreviewDto Preview, IReadOnlyList<RefundAllocation> Components);
