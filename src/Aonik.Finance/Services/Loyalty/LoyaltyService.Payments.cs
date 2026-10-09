using Aonik.Finance.Entities.Loyalty;
using Aonik.Finance.Entities.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Loyalty;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Loyalty;

internal sealed partial class LoyaltyService
{
    // These four methods participate in the payment caller's transaction and SaveChanges.
    public async Task<string?> PrepareTrackedAsync(PaymentIntent intent, LoyaltyCheckout? instruction, CancellationToken cancellationToken = default)
    {
        if (instruction is null)
        {
            await ValidateReplayAsync(intent, null, cancellationToken);
            return null;
        }
        await ValidateInstructionAsync(intent, instruction, cancellationToken);
        if (await AttemptAsync(intent.Id, cancellationToken) is not null)
        {
            await ValidateReplayAsync(intent, instruction, cancellationToken);
            return intent.FailureReason;
        }
        var attempt = new LoyaltyCheckoutAttempt
        {
            TenantId = TenantId, PaymentIntentId = intent.Id, OrderId = intent.OrderId, CartId = instruction.CartId,
            SnapshotJson = Serialize(instruction), Status = "Released"
        };
        db.LoyaltyCheckoutAttempts.Add(attempt);
        // A durable no-start cancellation still freezes the exact instruction, even after policy removal.
        if (intent.Status == "Cancelled") return null;
        LoyaltyPolicy policy;
        try { policy = await GetPolicyAsync(cancellationToken); }
        catch (InvalidStateException) { return "loyalty.unavailable"; }
        if (!policy.Enabled) return "loyalty.disabled";
        if (policy.Version != instruction.PolicyVersion || policy.Ledger != instruction.Ledger) return "loyalty.policy_changed";

        LoyaltyAccount account;
        try { account = await AccountAsync(instruction.PartyId, instruction.Ledger, cancellationToken); }
        catch (InvalidStateException) { return "loyalty.unavailable"; }
        var balance = await PostedPointsAsync(account.Id, cancellationToken);
        var reserved = await ReservedPointsAsync(account.Id, cancellationToken);
        if (instruction.RedeemedPoints > Math.Max(0, checked(balance - reserved))) return "loyalty.insufficient_points";
        attempt.AccountId = account.Id;
        attempt.ReservedPoints = instruction.RedeemedPoints;
        attempt.Status = "Reserved";
        Touch(account);
        return null;
    }

    public async Task ValidateReplayAsync(PaymentIntent intent, LoyaltyCheckout? instruction, CancellationToken cancellationToken = default)
    {
        if (intent.TenantId != TenantId) throw new InvalidStateException("The payment belongs to another tenant.");
        var attempt = await AttemptAsync(intent.Id, cancellationToken);
        if (attempt is null && instruction is null) return;
        if (attempt is null || instruction is null || attempt.OrderId != intent.OrderId
            || attempt.CartId != instruction.CartId || attempt.SnapshotJson != Serialize(instruction))
            throw new InvalidStateException("The payment attempt has a different frozen loyalty instruction.");
    }

    public async Task CompleteTrackedAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        var attempt = await AttemptAsync(intent.Id, cancellationToken);
        if (attempt is null) return;
        if (intent.Status != "Captured" || attempt.Status == "Released")
            throw new InvalidStateException("Only a captured reserved loyalty attempt can be completed.");
        var instruction = Read(attempt);
        await ValidateInstructionAsync(intent, instruction, cancellationToken);
        var account = await AccountAsync(instruction.PartyId, instruction.Ledger, cancellationToken);
        if (attempt.AccountId != account.Id) throw new InvalidStateException("The reserved loyalty owner changed.");
        var details = Serialize(new PaymentAward(intent.Id, instruction));
        await PostAsync(account, "Redeem", intent.Id, -instruction.RedeemedPoints, instruction.Ledger,
            instruction.Ledger.RedeemExpenseAccountId, intent.OrderId, null, details, null, cancellationToken);
        await PostAsync(account, "Earn", intent.OrderId, instruction.EarnedPoints, instruction.Ledger,
            instruction.Ledger.EarnExpenseAccountId, intent.OrderId, null, details, null, cancellationToken);
        attempt.Status = "Completed";
        Touch(account);
    }

    public async Task ReleaseTrackedAsync(PaymentIntent intent, CancellationToken cancellationToken = default)
    {
        var attempt = await AttemptAsync(intent.Id, cancellationToken);
        if (attempt is null || attempt.Status != "Reserved") return;
        if (intent.Status != "Cancelled") throw new InvalidStateException("A payable attempt must retain its loyalty reservation.");
        if (attempt.AccountId is { } id)
        {
            var account = await db.LoyaltyAccounts.SingleAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken);
            Touch(account);
        }
        attempt.Status = "Released";
    }

    private async Task<LoyaltyCheckoutAttempt?> AttemptAsync(Guid paymentIntentId, CancellationToken ct) =>
        db.LoyaltyCheckoutAttempts.Local.SingleOrDefault(x => x.TenantId == TenantId && x.PaymentIntentId == paymentIntentId)
        ?? await db.LoyaltyCheckoutAttempts.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.PaymentIntentId == paymentIntentId, ct);

    private async Task ValidateInstructionAsync(PaymentIntent intent, LoyaltyCheckout value, CancellationToken ct)
    {
        if (intent.TenantId != TenantId || intent.Currency != "GBP" || intent.OrderId == Guid.Empty
            || value.CartId == Guid.Empty || value.PartyId == Guid.Empty || value.PartyId != intent.PayerPartyId
            || value.Ledger is null || string.IsNullOrWhiteSpace(value.PolicyVersion)
            || value.RedeemedPoints < 0 || value.EarnedPoints < 0 || (value.IsGuest && value.RedeemedPoints != 0)
            || value.PayableTotal is null || value.PayableTotal != intent.Amount || value.OrderValueBeforePoints < 0
            || value.PointsAppliedValue != value.RedeemedPoints / 100m
            || value.RedeemedPoints > decimal.Floor(value.OrderValueBeforePoints * 20m)
            || value.Lines is null || value.Lines.Count is < 1 or > 500)
            throw new InvalidStateException("The frozen loyalty instruction does not match this payment.");
        if (value.Lines.Any(x => x is null || x.ItemIndex < 0 || x.OrderItemId == Guid.Empty)
            || value.Lines.Select(x => x.ItemIndex).Distinct().Count() != value.Lines.Count
            || value.Lines.Select(x => x.OrderItemId).Distinct().Count() != value.Lines.Count)
            throw new InvalidStateException("Loyalty allocations must reference distinct order lines.");
        foreach (var line in value.Lines)
        {
            if (line.OriginalCharged < 0 || line.CouponDiscount < 0 || line.PointsAppliedValue < 0 || line.GiftFundedValue < 0
                || line.NetPaidValue < 0 || line.EligibleEarnValue < 0 || line.EarnedPoints < 0 || line.RedeemedPoints < 0
                || line.OriginalCharged - line.CouponDiscount - line.PointsAppliedValue - line.GiftFundedValue != line.NetPaidValue
                || line.EligibleEarnValue > line.NetPaidValue
                || (!line.EarnEligible && (line.EligibleEarnValue != 0 || line.EarnedPoints != 0))
                || (!line.RedeemEligible && line.RedeemedPoints != 0))
                throw new InvalidStateException("The frozen loyalty line allocation is invalid.");
        }
        if (value.Lines.Sum(x => x.EarnedPoints) != value.EarnedPoints || value.Lines.Sum(x => x.RedeemedPoints) != value.RedeemedPoints
            || value.Lines.Sum(x => x.PointsAppliedValue) != value.PointsAppliedValue
            || value.EarnedPoints != decimal.Floor(value.Lines.Sum(x => x.EligibleEarnValue) * 2m))
            throw new InvalidStateException("The frozen loyalty allocation totals are invalid.");
        var ids = value.Lines.Select(x => x.OrderItemId).ToArray();
        var items = await db.OrderItems.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.OrderId == intent.OrderId && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (items.Count != ids.Length || value.Lines.Any(line => items[line.OrderItemId].ItemIndex != line.ItemIndex
                || items[line.OrderItemId].ItemType != line.ItemType || items[line.OrderItemId].AmountIn != line.OriginalCharged))
            throw new InvalidStateException("A loyalty allocation references an unavailable order line.");
        // The cap includes pre-benefit tax. PayableTotal includes recalculated tax, which need not be the same.
    }

    private sealed record PaymentAward(Guid PaymentIntentId, LoyaltyCheckout Checkout);
}
