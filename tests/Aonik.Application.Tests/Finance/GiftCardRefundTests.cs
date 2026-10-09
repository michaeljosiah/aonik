using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Services.GiftCards;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Payments;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Finance;

public sealed partial class GiftCardServiceTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    public async Task GiftRefund_Should_BlockNewFulfilmentSecretAfterPartialOrFullPurchaseReclaim(int amount)
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(30);
        var intent = await h.Db.PaymentIntents.SingleAsync(x => x.Id == issued.Card.Source.PaymentIntentId);
        var gift = (await h.Service.ReadRefundInstructionAsync(intent, 0, amount))!;
        var refund = NewGiftRefund(h, intent, gift, amount);
        h.Db.Refunds.Add(refund);
        await h.Service.PrepareRefundTrackedAsync(refund, gift);
        await h.Db.SaveChangesAsync();
        await h.Service.CompleteRefundTrackedAsync(refund, gift);
        refund.EffectsAppliedAtUtc = h.Clock.UtcNow;
        refund.Status = "Succeeded";
        await h.Db.SaveChangesAsync();

        await h.Service.Invoking(x => x.GetFulfilmentSecretAsync(issued.Card.Source))
            .Should().ThrowAsync<InvalidStateException>().WithMessage("*refund*support*");

        (await h.Service.GetBalanceAsync(issued.Code))!.Balance.Should().Be(30 - amount);
        (await h.Service.GetIssuedAsync(issued.Card.Source))!.FaceValue.Should().Be(30,
            "the original issuance remains immutable even though new delivery is blocked");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    public async Task GiftRefund_Should_StillReleaseOriginalSecretAfterOrdinarySpending(int amount)
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(30);
        await CaptureRefundTenderAsync(h, issued.Code, amount, amount);

        var resend = await h.Service.GetFulfilmentSecretAsync(issued.Card.Source);

        resend.Should().Be(issued);
        (await h.Service.GetBalanceAsync(issued.Code))!.Balance.Should().Be(30 - amount);
    }

    [Fact]
    public async Task GiftRefund_Should_RestoreOriginalTenderOnce_WithActualCreditAndOriginalBinding()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(100);
        var intent = await CaptureRefundTenderAsync(h, issued.Code, 80, 60);
        var original = await h.Db.GiftCardOperations.SingleAsync(x => x.Kind == "Redeem");
        var originalJournal = original.JournalEntryId;
        h.Raw = null;
        var gift = (await h.Service.ReadRefundInstructionAsync(intent, 12, 0))!;
        var refund = NewGiftRefund(h, intent, gift, 20);
        h.Db.Refunds.Add(refund);
        await h.Service.PrepareRefundTrackedAsync(refund, gift);
        await h.Db.SaveChangesAsync();

        await h.Service.CompleteRefundTrackedAsync(refund, gift);
        refund.EffectsAppliedAtUtc = h.Clock.UtcNow;
        refund.Status = "Succeeded";
        await h.Db.SaveChangesAsync();
        await h.Service.CompleteRefundTrackedAsync(refund, gift);
        await h.Db.SaveChangesAsync();

        (await h.Service.GetBalanceAsync(issued.Code))!.Balance.Should().Be(52);
        var restored = await h.Db.GiftCardOperations.SingleAsync(x => x.Kind == "Restore");
        restored.OriginalOperationId.Should().Be(original.Id);
        restored.PaymentId.Should().Be(original.PaymentId);
        var line = await h.Db.JournalEntryLines.SingleAsync(x => x.Id == restored.JournalEntryLineId);
        line.LedgerAccountId.Should().Be(h.Binding.LiabilityAccountId);
        line.Direction.Should().Be(JournalDirections.Credit);
        line.Amount.Should().Be(12);
        (await h.Db.JournalEntries.SingleAsync(x => x.Id == restored.JournalEntryId)).LedgerId.Should().Be(h.Binding.LedgerId);
        original.JournalEntryId.Should().Be(originalJournal);
        (await h.Db.Payments.CountAsync(x => x.PaymentIntentId == intent.Id)).Should().Be(2, "a refund is not a new captured payment");
        await h.Service.Invoking(x => x.ReleaseRefundTrackedAsync(refund, gift)).Should().ThrowAsync<InvalidStateException>();
    }

    [Fact]
    public async Task GiftRefund_Should_ReserveUnusedPurchaseValueBeforeProvider_AndBlockNewSpend()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(30);
        var spend = await h.TenderAsync(issued.Code, 25, 25);
        var intent = await h.Db.PaymentIntents.SingleAsync(x => x.Id == issued.Card.Source.PaymentIntentId);
        var gift = (await h.Service.ReadRefundInstructionAsync(intent, 0, 20))!;
        var refund = NewGiftRefund(h, intent, gift, 20);
        h.Db.Refunds.Add(refund);

        await h.Service.PrepareRefundTrackedAsync(refund, gift);
        await h.Db.SaveChangesAsync();
        refund.Status = "Unknown";
        await h.Db.SaveChangesAsync();

        var held = (await h.Service.GetBalanceAsync(issued.Code))!;
        held.Balance.Should().Be(30);
        held.Reserved.Should().Be(20);
        held.Available.Should().Be(10);
        await h.Service.Invoking(x => x.GetFulfilmentSecretAsync(issued.Card.Source))
            .Should().ThrowAsync<InvalidStateException>().WithMessage("*pending*refund*support*");
        (await h.Service.PrepareTrackedAsync(spend.Intent, spend.Instruction)).Should().Be("gift_card.insufficient_balance");
        await h.Db.SaveChangesAsync();
        await h.Service.ReleaseRefundTrackedAsync(refund, gift);
        refund.Status = "Failed";
        await h.Db.SaveChangesAsync();
        (await h.Service.GetBalanceAsync(issued.Code))!.Available.Should().Be(30);
        (await h.Service.GetFulfilmentSecretAsync(issued.Card.Source)).Code.Should().Be(issued.Code);
        await h.Service.Invoking(x => x.PrepareRefundTrackedAsync(refund, gift)).Should().ThrowAsync<InvalidStateException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GiftRefund_Should_RejectPurchaseReclaimOfSpentOrCheckoutReservedValue(bool spend)
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(30);
        var checkout = await h.TenderAsync(issued.Code, 20, 20);
        (await h.Service.PrepareTrackedAsync(checkout.Intent, checkout.Instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        if (spend)
        {
            checkout.Intent.Status = "Captured";
            await h.Service.CompleteTrackedAsync(checkout.Intent);
            await h.Db.SaveChangesAsync();
        }
        var original = await h.Db.PaymentIntents.SingleAsync(x => x.Id == issued.Card.Source.PaymentIntentId);
        var gift = (await h.Service.ReadRefundInstructionAsync(original, 0, 11))!;
        var refund = NewGiftRefund(h, original, gift, 11);

        await h.Service.Invoking(x => x.PrepareRefundTrackedAsync(refund, gift)).Should().ThrowAsync<InvalidStateException>();

        refund.GiftReclaimCardId.Should().BeNull();
        refund.GiftReclaimAmount.Should().Be(0);
        h.Db.GiftCardOperations.Any(x => x.Kind == "Reclaim").Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GiftRefund_Should_RejectExpiredAdmission_ButHonorAlreadyAcceptedObligation(bool restore)
    {
        using var h = await Harness.CreateAsync();
        h.Configure(h.Policy with { Validity = new(false, 1) });
        var issued = await h.IssueAsync(30);
        var intent = restore ? await CaptureRefundTenderAsync(h, issued.Code, 20, 20)
            : await h.Db.PaymentIntents.SingleAsync(x => x.Id == issued.Card.Source.PaymentIntentId);
        var gift = (await h.Service.ReadRefundInstructionAsync(intent, restore ? 10 : 0, restore ? 0 : 10))!;
        var accepted = NewGiftRefund(h, intent, gift, 10);
        h.Db.Refunds.Add(accepted);
        await h.Service.PrepareRefundTrackedAsync(accepted, gift);
        await h.Db.SaveChangesAsync();
        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(2);
        var late = NewGiftRefund(h, intent, gift, 10);
        await h.Service.Invoking(x => x.PrepareRefundTrackedAsync(late, gift))
            .Should().ThrowAsync<InvalidStateException>().WithMessage("*expired*support*");

        await h.Service.CompleteRefundTrackedAsync(accepted, gift);
        accepted.EffectsAppliedAtUtc = h.Clock.UtcNow;
        accepted.Status = "Succeeded";
        await h.Db.SaveChangesAsync();

        var balance = (await h.Service.GetBalanceAsync(issued.Code))!;
        balance.Balance.Should().Be(20);
        balance.Available.Should().Be(0);
        balance.Status.Should().Be("Expired");
        balance.ExpiresAtUtc.Should().Be(issued.Card.ExpiresAtUtc);
        var effect = await h.Db.GiftCardOperations.SingleAsync(x => x.SourceId == accepted.Id);
        effect.Kind.Should().Be(restore ? "Restore" : "Reclaim");
        effect.OriginalOperationId.Should().Be(gift.OriginalOperationId);
    }

    [Fact]
    public async Task GiftRefund_Should_CapPendingAndAppliedRestorations_AndRejectChangedReplayOrForeignSource()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(100);
        var intent = await CaptureRefundTenderAsync(h, issued.Code, 80, 60);
        var gift = (await h.Service.ReadRefundInstructionAsync(intent, 40, 0))!;
        var first = NewGiftRefund(h, intent, gift, 40);
        h.Db.Refunds.Add(first);
        await h.Service.PrepareRefundTrackedAsync(first, gift);
        await h.Db.SaveChangesAsync();
        var tooMuch = gift with { Amount = 21 };
        var second = NewGiftRefund(h, intent, tooMuch, 21);
        await h.Service.Invoking(x => x.PrepareRefundTrackedAsync(second, tooMuch)).Should().ThrowAsync<InvalidStateException>();
        await h.Service.CompleteRefundTrackedAsync(first, gift);
        first.EffectsAppliedAtUtc = h.Clock.UtcNow;
        first.Status = "Succeeded";
        await h.Db.SaveChangesAsync();
        await h.Service.Invoking(x => x.PrepareRefundTrackedAsync(second, tooMuch)).Should().ThrowAsync<InvalidStateException>();
        await h.Service.Invoking(x => x.CompleteRefundTrackedAsync(first, gift with { Amount = 39 })).Should().ThrowAsync<InvalidStateException>();
        var foreign = NewGiftRefund(h, intent, gift with { GiftCardId = Guid.NewGuid() }, 40);
        await h.Service.Invoking(x => x.PrepareRefundTrackedAsync(foreign, RefundSnapshot.Read(foreign).Gift)).Should().ThrowAsync<InvalidStateException>();
        h.Db.GiftCardOperations.Count(x => x.Kind == "Restore").Should().Be(1);
    }

    private static async Task<PaymentIntent> CaptureRefundTenderAsync(Harness h, string code, decimal total, decimal gift)
    {
        var checkout = await h.TenderAsync(code, total, gift);
        (await h.Service.PrepareTrackedAsync(checkout.Intent, checkout.Instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        if (total > gift) await h.RecordCashAsync(checkout.Intent, total - gift);
        checkout.Intent.Status = "Captured";
        await h.Service.CompleteTrackedAsync(checkout.Intent);
        await h.Db.SaveChangesAsync();
        return checkout.Intent;
    }

    private static Refund NewGiftRefund(Harness h, PaymentIntent intent, GiftCardRefundInstruction gift, decimal amount)
    {
        var refund = new Refund { TenantId = h.TenantId, PaymentIntentId = intent.Id, Currency = "GBP", Amount = amount, Status = "Requested" };
        var source = new CheckoutRefundSource(intent.OrderId, intent.Id, null, "GBP", intent.Amount, 0, intent.Amount, []);
        refund.RequestSnapshotJson = new RefundSnapshot(source, new RefundRequest(refund.Id, "Return", [], "version"), null, [],
            amount - (gift.Kind == GiftCardRefundInstruction.RestoreTender ? gift.Amount : 0),
            gift.Kind == GiftCardRefundInstruction.RestoreTender ? gift.Amount : 0, null, null, gift, null).Serialize();
        return refund;
    }
}
