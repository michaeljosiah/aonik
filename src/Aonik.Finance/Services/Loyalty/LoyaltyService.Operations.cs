using System.Text.Json;

using Aonik.Finance.Entities.Loyalty;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Events.Integration;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Finance.Services.Loyalty;

internal sealed partial class LoyaltyService
{
    public Task<LoyaltyOperationRef> AdjustAsync(LoyaltyAdjustment command, CancellationToken cancellationToken = default)
    {
        if (command.PartyId == Guid.Empty || command.AdjustmentId == Guid.Empty || command.Points == 0
            || string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length > 500 || command.Reason.Any(char.IsControl))
            throw new InvalidStateException("An adjustment requires an owner, stable identifier, nonzero points and a reason.");
        command = command with { Reason = command.Reason.Trim() };
        return InTransactionAsync(async ct =>
        {
            var details = Serialize(command);
            var existing = await OperationAsync("Adjust", command.AdjustmentId, ct);
            if (existing is not null)
            {
                if (existing.DetailsJson != details) throw new InvalidStateException("The adjustment was already recorded with different facts.");
                return new LoyaltyOperationRef(existing.Id, existing.Points, existing.JournalEntryId);
            }
            var policy = await GetPolicyAsync(ct);
            if (!policy.Enabled || policy.Ledger is null) throw new InvalidStateException("Loyalty is not enabled.");
            var account = await AccountAsync(command.PartyId, policy.Ledger, ct);
            var operation = await PostAsync(account, "Adjust", command.AdjustmentId, command.Points, policy.Ledger,
                policy.Ledger.EarnExpenseAccountId, null, null, details, command.Reason, ct);
            return new LoyaltyOperationRef(operation.Id, operation.Points, operation.JournalEntryId);
        }, cancellationToken);
    }

    public async Task ReverseRefundAsync(LoyaltyRefund command, CancellationToken cancellationToken = default)
    {
        await InTransactionAsync(async ct =>
        {
            await ReverseRefundTrackedAsync(command, ct);
            return true;
        }, cancellationToken);
    }

    internal async Task ReverseRefundTrackedAsync(LoyaltyRefund command, CancellationToken ct = default)
    {
        if (command.RefundId == Guid.Empty || command.OrderId == Guid.Empty || command.PaymentIntentId == Guid.Empty
            || command.Lines is null || command.Lines.Count is < 1 or > 500
            || command.Lines.Any(x => x is null || x.OrderItemId == Guid.Empty || x.EarnedPointsToReverse < 0 || x.RedeemedPointsToRestore < 0)
            || command.Lines.Select(x => x.OrderItemId).Distinct().Count() != command.Lines.Count
            || !command.Lines.Any(x => x.EarnedPointsToReverse > 0 || x.RedeemedPointsToRestore > 0))
            throw new InvalidStateException("The refund must reference original order line point allocations.");
        command = command with { Lines = command.Lines.OrderBy(x => x.OrderItemId).ToArray() };
        var attempt = await AttemptAsync(command.PaymentIntentId, ct);
        if (attempt is null || attempt.OrderId != command.OrderId || attempt.Status != "Completed")
            throw new InvalidStateException("The original completed loyalty payment is unavailable.");
        var original = Read(attempt);
        var details = Serialize(command);
        var prior = await OperationAsync("EarnReverse", command.RefundId, ct);
        if (prior is not null)
        {
            var restored = await OperationAsync("RedemptionRestore", command.RefundId, ct);
            if (prior.DetailsJson != details || restored?.DetailsJson != details)
                throw new InvalidStateException("The refund was already recorded with different facts.");
            return;
        }
        var earned = await OperationAsync("Earn", command.OrderId, ct)
            ?? throw new InvalidStateException("The original loyalty award is unavailable.");
        var redeemed = await OperationAsync("Redeem", command.PaymentIntentId, ct)
            ?? throw new InvalidStateException("The original loyalty redemption is unavailable.");
        // Capture the version BEFORE reading cumulative refunds or a later owner claim.
        // Every writer of those facts touches this owner; a concurrent change forces a fresh calculation.
        var originalOwner = await OwnedAccountAsync(earned.AccountId, ct);
        var previousJson = await db.LoyaltyOperations.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Kind == "EarnReverse" && x.OrderId == command.OrderId)
            .Select(x => x.DetailsJson).ToListAsync(ct);
        var previous = previousJson.Select(x => JsonSerializer.Deserialize<LoyaltyRefund>(x, Json)
            ?? throw new InvalidStateException("An original refund allocation is unavailable.")).ToArray();
        foreach (var line in command.Lines)
        {
            var saved = original.Lines.SingleOrDefault(x => x.OrderItemId == line.OrderItemId)
                ?? throw new InvalidStateException("The refund line does not belong to this payment.");
            var earlier = previous.SelectMany(x => x.Lines).Where(x => x.OrderItemId == line.OrderItemId).ToArray();
            if (checked(earlier.Sum(x => x.EarnedPointsToReverse) + line.EarnedPointsToReverse) > saved.EarnedPoints
                || checked(earlier.Sum(x => x.RedeemedPointsToRestore) + line.RedeemedPointsToRestore) > saved.RedeemedPoints)
                throw new InvalidStateException("The refund exceeds the original line point allocation.");
        }
        var claim = await OperationAsync("ClaimIn", earned.Id, ct);
        var earnOwner = claim is null ? originalOwner : await OwnedAccountAsync(claim.AccountId, ct);
        var redeemOwner = await OwnedAccountAsync(redeemed.AccountId, ct);
        // Claim and reversal always contend on the original owner's native version.
        Touch(originalOwner);
        await PostAsync(earnOwner, "EarnReverse", command.RefundId, -command.Lines.Sum(x => x.EarnedPointsToReverse), original.Ledger,
            original.Ledger.EarnExpenseAccountId, command.OrderId, earned.Id, details, null, ct);
        await PostAsync(redeemOwner, "RedemptionRestore", command.RefundId, command.Lines.Sum(x => x.RedeemedPointsToRestore), original.Ledger,
            original.Ledger.RedeemExpenseAccountId, command.OrderId, redeemed.Id, details, null, ct);
    }

    public async Task AttachVerifiedGuestAsync(AccountAccessVerifiedEvent verified, CancellationToken cancellationToken = default)
    {
        if (verified.TenantId != TenantId || verified.ActionId == Guid.Empty || verified.CartId == Guid.Empty
            || verified.OrderId == Guid.Empty || verified.PaymentIntentId == Guid.Empty || verified.GuestPartyId == Guid.Empty
            || verified.AccountPartyId == Guid.Empty || verified.GuestPartyId == verified.AccountPartyId)
            throw new InvalidStateException("The verified loyalty attachment is invalid.");
        await InTransactionAsync(async ct =>
        {
            var attempt = await AttemptAsync(verified.PaymentIntentId, ct);
            if (attempt is null) return true; // Historical payments have no loyalty instruction or award.
            var instruction = Read(attempt);
            if (attempt.Status != "Completed" || attempt.OrderId != verified.OrderId || attempt.CartId != verified.CartId
                || !instruction.IsGuest || instruction.PartyId != verified.GuestPartyId)
                throw new InvalidStateException("The verified guest payment has no completed matching loyalty award.");
            var earned = await OperationAsync("Earn", verified.OrderId, ct)
                ?? throw new InvalidStateException("The expected guest loyalty award has not been recorded.");
            var prior = await OperationAsync("ClaimOut", earned.Id, ct);
            if (prior is not null)
            {
                var incoming = await OperationAsync("ClaimIn", earned.Id, ct);
                if (incoming is null || (await OwnedAccountAsync(incoming.AccountId, ct)).PartyId != verified.AccountPartyId)
                    throw new InvalidStateException("The guest award was already attached to a different account.");
                return true;
            }
            var guest = await OwnedAccountAsync(earned.AccountId, ct);
            if (guest.PartyId != verified.GuestPartyId) throw new InvalidStateException("The original award owner does not match.");
            var account = await AccountAsync(verified.AccountPartyId, instruction.Ledger, ct);
            var relevant = ActivityQuery(guest.Id).Where(x => x.Id == earned.Id || (x.Kind == "EarnReverse" && x.OriginalOperationId == earned.Id));
            var remaining = AsPoints(await relevant.SumAsync(x => x.SignedAmount, ct));
            if (remaining < 0) throw new InvalidStateException("The guest award reversals exceed the original award.");
            Touch(guest);
            Touch(account);
            var details = Serialize(new { verified.ActionId, verified.CartId, verified.OrderId, verified.PaymentIntentId, verified.GuestPartyId, verified.AccountPartyId });
            var outgoing = NewOperation(guest, "ClaimOut", earned.Id, -remaining, verified.OrderId, earned.Id, details);
            var incomingOperation = NewOperation(account, "ClaimIn", earned.Id, remaining, verified.OrderId, earned.Id, details);
            if (remaining != 0)
            {
                var accounts = await BindingAsync(instruction.Ledger, ct);
                var code = accounts[instruction.Ledger.LiabilityAccountId].Code;
                var amount = remaining / 100m;
                var posted = await journals.PostAsync(new(instruction.Ledger.LedgerId, "LoyaltyClaim", earned.Id,
                [
                    new(code, JournalDirections.Debit, amount, "GBP", null, Serialize(new { loyaltyAccountId = guest.Id, operationId = outgoing.Id })),
                    new(code, JournalDirections.Credit, amount, "GBP", null, Serialize(new { loyaltyAccountId = account.Id, operationId = incomingOperation.Id }))
                ], clock.UtcNow), ct);
                outgoing.JournalEntryId = incomingOperation.JournalEntryId = posted.JournalEntryId;
                outgoing.JournalEntryLineId = await FindLegAsync(posted.JournalEntryId, instruction.Ledger, JournalDirections.Debit, amount, ct);
                incomingOperation.JournalEntryLineId = await FindLegAsync(posted.JournalEntryId, instruction.Ledger, JournalDirections.Credit, amount, ct);
            }
            db.LoyaltyOperations.AddRange(outgoing, incomingOperation);
            return true;
        }, cancellationToken);
    }

    private Task<LoyaltyAccount> OwnedAccountAsync(Guid id, CancellationToken ct) =>
        db.LoyaltyAccounts.SingleAsync(x => x.TenantId == TenantId && x.Id == id, ct);
}
