using Aonik.Finance.Entities.Orders;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Finance;
public sealed partial class GiftCardServiceTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    public async Task MultiPurchase_IssuesDistinctCardsAndJournals_OnlyAfterCashProof_AndReplaysWithoutDuplicates(int quantity)
    {
        using var h = await Harness.CreateAsync();
        var (intent, first) = await h.PurchaseAsync(50, quantity * 50m);
        var more = Enumerable.Range(1, quantity - 1).Select(index => new OrderItem {
            TenantId = h.TenantId, OrderId = intent.OrderId, ItemIndex = index, ItemType = "GiftCardValue",
            Quantity = 1m, UnitPrice = 50m, AmountIn = 50m, CurrencyIn = "GBP" }).ToArray();
        h.Db.OrderItems.AddRange(more); await h.Db.SaveChangesAsync();
        var instruction = first with { AdditionalPurchases = more.Select(item => new GiftCardPurchase(item.ItemIndex, item.Id, 50m)).ToArray() };
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        h.Db.GiftCards.Should().BeEmpty();
        await h.RecordCashAsync(intent, quantity * 50m);
        intent.Status = "Captured";
        await h.Service.CompleteTrackedAsync(intent); await h.Db.SaveChangesAsync();
        await h.Service.CompleteTrackedAsync(intent); await h.Db.SaveChangesAsync();
        var cards = await h.Db.GiftCards.ToListAsync();
        cards.Should().HaveCount(quantity);
        cards.Select(card => card.CodeHash).Distinct().Should().HaveCount(quantity);
        cards.Select(card => card.OrderItemId).Distinct().Should().HaveCount(quantity);
        cards.Sum(card => card.FaceValue).Should().Be(quantity * 50m);
        (await h.Db.GiftCardOperations.CountAsync(x => x.Kind == "Issue")).Should().Be(quantity);
        (await h.Db.JournalEntries.CountAsync(x => x.SourceType == "GiftCardIssue")).Should().Be(quantity);
        await h.Service.Invoking(service => service.ReadRefundInstructionAsync(intent, 0, 10))
            .Should().ThrowAsync<InvalidStateException>().WithMessage("*identify*");
        var selected = cards.Last();
        var selectedSecret = (await h.Service.GetFulfilmentSecretAsync(new(selected.CartId, selected.OrderId, selected.PaymentIntentId, selected.OrderItemId, selected.ItemIndex)))!;
        var reclaim = (await h.Service.ReadRefundInstructionAsync(intent, 0, 10, orderItemId: selected.OrderItemId))!;
        reclaim.GiftCardId.Should().Be(selected.Id);
        var refund = NewGiftRefund(h, intent, reclaim, 10);
        h.Db.Refunds.Add(refund);
        await h.Service.PrepareRefundTrackedAsync(refund, reclaim); await h.Db.SaveChangesAsync();
        await h.Service.CompleteRefundTrackedAsync(refund, reclaim);
        refund.EffectsAppliedAtUtc = h.Clock.UtcNow; refund.Status = "Succeeded";
        await h.Db.SaveChangesAsync();
        await h.Service.CompleteRefundTrackedAsync(refund, reclaim); await h.Db.SaveChangesAsync();
        (await h.Db.GiftCardOperations.CountAsync(x => x.Kind == "Reclaim")).Should().Be(1);
        (await h.Service.GetBalanceAsync(selectedSecret.Code))!.Balance.Should().Be(40);
        (await h.Service.GetIssuedAsync(new(selected.CartId, selected.OrderId, selected.PaymentIntentId, selected.OrderItemId, selected.ItemIndex)))!.FaceValue.Should().Be(50);
        foreach (var card in cards)
        {
            if (card.Id != selected.Id) {
            var secret = await h.Service.GetFulfilmentSecretAsync(new(card.CartId, card.OrderId, card.PaymentIntentId, card.OrderItemId, card.ItemIndex));
                (await h.Service.GetBalanceAsync(secret!.Code))!.Balance.Should().Be(50);
            }
            (await h.Service.GetIssuedAsync(new(card.CartId, card.OrderId, card.PaymentIntentId, card.OrderItemId, card.ItemIndex))).Should().NotBeNull();
        }
    }
}
