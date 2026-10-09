using System.Text.Json;

using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Billing;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.GiftCards;
using Aonik.Finance.Services.Ledger;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Moq;

using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;

namespace Aonik.Application.Tests.Finance;

public sealed class GiftCardServiceTests
{
    [Fact]
    public async Task Policy_Should_DefaultDisabled_AndFingerprintAllAuthoredAccountingTerms()
    {
        using var h = await Harness.CreateAsync();
        var original = await h.Service.GetPolicyAsync();
        h.Configure(h.Policy with { TermsVersion = "terms-v2" });
        (await h.Service.GetPolicyAsync()).Version.Should().NotBe(original.Version);
        h.Raw = null;
        (await h.Service.GetPolicyAsync()).Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("validity")]
    [InlineData("allocation")]
    [InlineData("currency")]
    [InlineData("foreign-account")]
    public async Task Policy_Should_RejectMissingOrIncompatibleExplicitConfiguration(string kind)
    {
        using var h = await Harness.CreateAsync();
        h.Configure(kind switch
        {
            "validity" => h.Policy with { Validity = new(false) },
            "allocation" => h.Policy with { FundingAllocation = null },
            "currency" => h.Policy with { Currency = "USD" },
            _ => h.Policy with { Ledger = h.Binding with { LiabilityAccountId = Guid.NewGuid() } }
        });
        await h.Service.Invoking(x => x.GetPolicyAsync()).Should().ThrowAsync<InvalidStateException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateAccountCode_Should_RejectPolicyAndPaymentAdmissionBeforeReserving(bool redeem)
    {
        using var h = await Harness.CreateAsync();
        var checkout = redeem
            ? await h.TenderAsync((await h.IssueAsync(100)).Code, 80, 60)
            : await h.PurchaseAsync(50);
        var journalCount = await h.Db.JournalEntries.CountAsync();
        h.Db.LedgerAccounts.Add(new LedgerAccount { TenantId = h.TenantId, LedgerId = h.Binding.LedgerId,
            Code = "gift-liability", AccountType = "Liability" });
        await h.Db.SaveChangesAsync();

        await h.Service.Invoking(x => x.GetPolicyAsync()).Should().ThrowAsync<InvalidStateException>();
        (await h.Service.PrepareTrackedAsync(checkout.Intent, checkout.Instruction)).Should().Be("gift_card.unavailable");
        await h.Db.SaveChangesAsync();

        var attempt = await h.Db.GiftCardCheckoutAttempts.SingleAsync(x => x.PaymentIntentId == checkout.Intent.Id);
        attempt.Status.Should().Be("Released");
        attempt.ReservedAmount.Should().Be(0);
        (await h.Db.JournalEntries.CountAsync()).Should().Be(journalCount);
        h.Db.Payments.Any(x => x.PaymentIntentId == checkout.Intent.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Purchase_Should_IssueOnlyAfterRealCashProof_AndReplaySameSecretAndJournal()
    {
        using var h = await Harness.CreateAsync();
        var (intent, instruction) = await h.PurchaseAsync(50);
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        await h.Service.Invoking(x => x.CompleteTrackedAsync(intent)).Should().ThrowAsync<InvalidStateException>();
        intent.Status = "Captured";
        await h.Service.Invoking(x => x.CompleteTrackedAsync(intent)).Should().ThrowAsync<InvalidStateException>();
        h.Db.GiftCards.Should().BeEmpty();
        await h.RecordCashAsync(intent, 50);
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();
        var source = Harness.Source(intent, instruction);
        var first = await h.Service.GetFulfilmentSecretAsync(source);
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();
        var replay = await h.Service.GetFulfilmentSecretAsync(source);

        replay.Should().Be(first);
        first.Code.Should().HaveLength(32);
        (await h.Service.GetBalanceAsync(first.Code))!.Balance.Should().Be(50);
        var stored = await h.Db.GiftCards.SingleAsync();
        stored.CodeHash.Should().NotContain(first.Code);
        stored.ProtectedCode.Should().NotContain(first.Code);
        h.Db.GiftCardOperations.Should().ContainSingle(x => x.Kind == "Issue");
        h.Db.JournalEntries.Count(x => x.SourceType == "GiftCardIssue").Should().Be(1);
        var issued = await h.Db.Set<OutboxMessage>().Where(x => x.EventType == typeof(GiftCardIssuedEvent).FullName).ToListAsync();
        issued.Should().ContainSingle();
        issued[0].Payload.Should().NotContain(first.Code);
        (await h.Service.GetIssuedAsync(source with { OrderId = Guid.NewGuid() })).Should().BeNull();
    }

    [Fact]
    public async Task Tender_Should_RedeemOriginalBindingAfterIssuanceDisabled_WithActualMixedReceipts()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(100);
        h.Raw = null;
        var (intent, instruction) = await h.TenderAsync(issued.Code, total: 80, requested: 60);
        instruction.Ledger.Should().Be(h.Binding);
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        var held = (await h.Service.GetBalanceAsync(issued.Code))!;
        held.Balance.Should().Be(100);
        held.Reserved.Should().Be(60);
        held.Available.Should().Be(40);
        await h.RecordCashAsync(intent, 20);
        intent.Status = "Captured";
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();

        var receipts = await h.Db.Payments.Where(x => x.PaymentIntentId == intent.Id).ToListAsync();
        receipts.Should().HaveCount(2);
        receipts.Single(x => x.Provider == "Stripe").Amount.Should().Be(20);
        receipts.Single(x => x.Provider == "GiftCard").Amount.Should().Be(60);
        receipts.Sum(x => x.Amount).Should().Be(80);
        var balance = (await h.Service.GetBalanceAsync(issued.Code))!;
        balance.Balance.Should().Be(40);
        balance.Reserved.Should().Be(0);
        h.Db.GiftCardOperations.Count(x => x.Kind == "Redeem").Should().Be(1);
    }

    [Fact]
    public async Task GiftOnly_Should_ConsumeRealReservedLiabilityWithoutCashReceipt()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(30);
        var (intent, instruction) = await h.TenderAsync(issued.Code, 30, 30);
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        intent.Status = "Captured";
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();

        (await h.Service.ReadFundingTrackedAsync(intent)).CardAmount.Should().Be(0);
        (await h.Db.Payments.Where(x => x.PaymentIntentId == intent.Id).SingleAsync()).Provider.Should().Be("GiftCard");
        h.Db.JournalEntries.Any(x => x.SourceId == intent.Id && x.SourceType == "PaymentCapture").Should().BeFalse();
        (await h.Service.GetBalanceAsync(issued.Code))!.Available.Should().Be(0);
    }

    [Fact]
    public async Task Quote_Should_AllocateNetLineAndTaxShares_WithoutChangingSaleTotal()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(100);
        var cart = Guid.NewGuid();
        var grant = (await h.Service.AuthorizeForCartAsync(issued.Code, cart)).PrivateCartGrant!;
        var quote = await h.Service.QuoteAsync(new(cart, grant, "GBP", 12, 2, 6,
            [new(0, Guid.Empty, "ProductPurchase", 12, 1, 1)]));

        quote.CardAmount.Should().Be(6);
        quote.Checkout!.Tender!.Lines[0].GiftFundedValue.Should().Be(5);
        quote.Checkout.Tender.GiftFundedTaxAmount.Should().Be(1);
        var tiny = await h.Service.QuoteAsync(new(cart, grant, "GBP", 1, 0, .01m,
            [new(0, Guid.Empty, "ProductPurchase", .5m, 0, 0), new(1, Guid.Empty, "ProductPurchase", .5m, 0, 0)]));
        tiny.Checkout!.Tender!.Lines.Select(x => x.GiftFundedValue).Should().Equal(.005m, .005m);
    }

    [Fact]
    public async Task Quote_Should_RequireAnotherChoiceInsteadOfSilentlyChangingCardAmount()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(10);
        var cart = Guid.NewGuid();
        var grant = (await h.Service.AuthorizeForCartAsync(issued.Code, cart)).PrivateCartGrant!;
        var insufficient = await h.Service.QuoteAsync(new(cart, grant, "GBP", 20, 0, 15, [new(0, Guid.Empty, "ProductPurchase", 20, 0, 0)]));
        insufficient.Checkout.Should().BeNull();
        insufficient.ReasonCode.Should().Be("gift_card.insufficient_balance");
        insufficient.MaxRedeemableAmount.Should().Be(10);
        var tooSmall = await h.Service.QuoteAsync(new(cart, grant, "GBP", 10.1m, 0, 10, [new(0, Guid.Empty, "ProductPurchase", 10.1m, 0, 0)]));
        tooSmall.Checkout.Should().BeNull();
        tooSmall.ReasonCode.Should().Be("gift_card.card_amount_too_small");
    }

    [Fact]
    public async Task ReducedCartTotal_Should_ReturnTenderRejection_WhileAdmissionRejectsUnmatchedFunding()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(100);
        var (intent, instruction) = await h.TenderAsync(issued.Code, 30, 20);
        var tender = instruction.Tender!;

        // The selected £20 is retained after the shopper reduces the cart to £10.
        var quote = await h.Service.QuoteAsync(new(instruction.CartId, tender.PrivateCartGrant, "GBP", 10, 0, 20,
            [new(0, Guid.Empty, "ProductPurchase", 10, 0, 0)]));

        quote.RequestedAmount.Should().Be(20);
        quote.MaxRedeemableAmount.Should().Be(10);
        quote.Checkout.Should().BeNull();
        quote.GiftAmount.Should().Be(0);
        quote.CardAmount.Should().Be(10);
        quote.ReasonCode.Should().Be("gift_card.insufficient_balance");
        (await h.Service.GetBalanceAsync(issued.Code))!.Reserved.Should().Be(0);

        intent.Amount = 10;
        await h.Service.Invoking(x => x.PrepareTrackedAsync(intent, instruction with
            { Tender = tender with { ExpectedCardAmount = 0 } })).Should().ThrowAsync<InvalidStateException>();
        h.Db.GiftCardCheckoutAttempts.Any(x => x.PaymentIntentId == intent.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Reservation_Should_RejectAStaleQuote_AndReleaseOnlyAfterConfirmedCancellation()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(100);
        var first = await h.TenderAsync(issued.Code, 80, 60);
        var second = await h.TenderAsync(issued.Code, 80, 60);
        (await h.Service.PrepareTrackedAsync(first.Intent, first.Instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        (await h.Service.PrepareTrackedAsync(second.Intent, second.Instruction)).Should().Be("gift_card.insufficient_balance");
        await h.Db.SaveChangesAsync();
        await h.Service.Invoking(x => x.ReleaseTrackedAsync(first.Intent)).Should().ThrowAsync<InvalidStateException>();
        (await h.Service.GetBalanceAsync(issued.Code))!.Reserved.Should().Be(60);
        first.Intent.Status = "Cancelled";
        await h.Service.ReleaseTrackedAsync(first.Intent);
        await h.Db.SaveChangesAsync();
        (await h.Service.GetBalanceAsync(issued.Code))!.Available.Should().Be(100);
        h.Db.GiftCardCheckoutAttempts.Where(x => x.PaymentIntentId == second.Intent.Id).Single().Status.Should().Be("Released");
    }

    [Fact]
    public async Task Capability_Should_BindTenantAndCart_AndNeverActAsPurchaserIdentity()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(50);
        var cart = Guid.NewGuid();
        var grant = (await h.Service.AuthorizeForCartAsync(issued.Code, cart)).PrivateCartGrant!;
        var request = new GiftCardQuoteRequest(cart, grant, "GBP", 10, 0, 5, [new(0, Guid.Empty, "ProductPurchase", 10, 0, 0)]);
        (await h.Service.QuoteAsync(request with { CartId = Guid.NewGuid() })).ReasonCode.Should().Be("gift_card.invalid");
        (await h.Service.QuoteAsync(request with { PrivateCartGrant = grant + "x" })).ReasonCode.Should().Be("gift_card.invalid");
        (await h.Service.GetBalanceAsync(new string('0', 32))).Should().BeNull();
        using var other = h.OtherTenant();
        var service = new GiftCardService(other.Db, other.Tenant, h.Clock, h.Settings.Object, new JournalWriter(other.Db, other.Tenant, h.Clock), h.Protection);
        (await service.GetBalanceAsync(issued.Code)).Should().BeNull();
        (await service.QuoteAsync(request)).ReasonCode.Should().Be("gift_card.invalid");
        await service.Invoking(x => x.GetFulfilmentSecretAsync(issued.Card.Source)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Expiry_Should_BlockNewSpend_ButRetainAlreadyReservedPaymentFunding()
    {
        using var h = await Harness.CreateAsync();
        h.Configure(h.Policy with { Validity = new(false, 1) });
        var issued = await h.IssueAsync(30);
        var (intent, instruction) = await h.TenderAsync(issued.Code, 30, 30);
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        h.Clock.UtcNow = h.Clock.UtcNow.AddDays(2);
        (await h.Service.AuthorizeForCartAsync(issued.Code, Guid.NewGuid())).ReasonCode.Should().Be("gift_card.expired");
        (await h.Service.GetBalanceAsync(issued.Code))!.Available.Should().Be(0);
        intent.Status = "Captured";
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();
        (await h.Service.GetBalanceAsync(issued.Code))!.Balance.Should().Be(0);
    }

    [Fact]
    public async Task ExpiredFirstAttempt_Should_FreezeTombstoneWithoutCurrentPolicy_AndReplayExactly()
    {
        using var h = await Harness.CreateAsync();
        var (intent, instruction) = await h.PurchaseAsync(20);
        intent.Status = "Cancelled";
        h.Settings.Reset();
        h.Settings.Setup(x => x.GetTenantValueAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Must not consult live settings"));
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        await h.Service.ValidateReplayAsync(intent, instruction);
        await h.Service.Invoking(x => x.ValidateReplayAsync(intent, null)).Should().ThrowAsync<InvalidStateException>();
        await h.Service.Invoking(x => x.ValidateReplayAsync(intent, instruction with { TermsVersion = "changed" })).Should().ThrowAsync<InvalidStateException>();
        (await h.Db.GiftCardCheckoutAttempts.SingleAsync()).Status.Should().Be("Released");
        h.Db.GiftCards.Should().BeEmpty();
        h.Db.JournalEntries.Should().BeEmpty();
    }

    [Fact]
    public async Task Validation_Should_RejectGiftToGiftAndTamperedActualOrderFacts()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(100);
        var tender = await h.TenderAsync(issued.Code, 50, 30);
        var purchase = await h.PurchaseAsync(50);
        await h.Service.Invoking(x => x.PrepareTrackedAsync(purchase.Intent, purchase.Instruction with { Tender = tender.Instruction.Tender }))
            .Should().ThrowAsync<InvalidStateException>();
        await h.Service.Invoking(x => x.PrepareTrackedAsync(purchase.Intent, null)).Should().ThrowAsync<InvalidStateException>();
        var wrong = tender.Instruction with { Tender = tender.Instruction.Tender! with
            { Lines = [tender.Instruction.Tender.Lines[0] with { ItemType = "GreetingCard" }] } };
        await h.Service.Invoking(x => x.PrepareTrackedAsync(tender.Intent, wrong)).Should().ThrowAsync<InvalidStateException>();
        var wrongFunding = tender.Instruction with { Tender = tender.Instruction.Tender with { ExpectedCardAmount = 19 } };
        await h.Service.Invoking(x => x.PrepareTrackedAsync(tender.Intent, wrongFunding)).Should().ThrowAsync<InvalidStateException>();
        var discountedFace = purchase.Instruction with { Purchase = purchase.Instruction.Purchase! with { FaceValue = 40 } };
        await h.Service.Invoking(x => x.PrepareTrackedAsync(purchase.Intent, discountedFace)).Should().ThrowAsync<InvalidStateException>();
    }

    [Fact]
    public async Task Balance_Should_FailClosedForCorruptedJournalLeg()
    {
        using var h = await Harness.CreateAsync();
        var issued = await h.IssueAsync(50);
        var operation = await h.Db.GiftCardOperations.SingleAsync();
        var leg = await h.Db.JournalEntryLines.SingleAsync(x => x.Id == operation.JournalEntryLineId);
        leg.Amount = 999;
        await h.Db.SaveChangesAsync();
        await h.Service.Invoking(x => x.GetBalanceAsync(issued.Code)).Should().ThrowAsync<InvalidStateException>();
    }

    [Fact]
    public async Task InvoiceFacts_Should_FailBeforeCapture_AndNeutralizeOnlyExactFundedGiftValue()
    {
        using var h = await Harness.CreateAsync();
        var (intent, instruction) = await h.PurchaseAsync(50, total: 60);
        Func<Task> beforeCapture = () => GiftCardInvoiceSettlement.ReadAsync(h.Db, h.TenantId, intent.OrderId, 60, "GBP");
        await beforeCapture.Should().ThrowAsync<InvalidStateException>();
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        await h.RecordCashAsync(intent, 60);
        intent.Status = "Captured";
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();
        var facts = await GiftCardInvoiceSettlement.ReadAsync(h.Db, h.TenantId, intent.OrderId, 60, "GBP");
        facts!.GiftValue.Should().Be(50);
        facts.Ledger.Should().Be(h.Binding);
        Func<Task> wrongInvoice = () => GiftCardInvoiceSettlement.ReadAsync(h.Db, h.TenantId, intent.OrderId, 50, "GBP");
        await wrongInvoice.Should().ThrowAsync<InvalidStateException>();
    }

    [Theory]
    [InlineData(80)]
    [InlineData(60)]
    public async Task InvoiceSettlement_Should_ClearActualPureOrMixedGiftFunding_InOriginalLedger(decimal giftAmount)
    {
        using var h = await Harness.CreateAsync();
        var revenueId = await h.AddInvoiceAccountsAsync();
        var issued = await h.IssueAsync(100);
        var (intent, instruction) = await h.TenderAsync(issued.Code, 80, giftAmount);
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        if (giftAmount < 80) await h.RecordCashAsync(intent, 80 - giftAmount);
        intent.Status = "Captured";
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();
        var invoice = new Invoice { TenantId = h.TenantId, OrderId = intent.OrderId, Total = 80, Currency = "GBP" };
        var service = new LedgerPostingService(h.Db);

        await service.PostInvoiceSettlementAsync(invoice);
        await service.PostInvoiceSettlementAsync(invoice);

        var journal = await h.Db.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.SourceType == "InvoiceSettlement" && x.SourceId == invoice.Id);
        journal.LedgerId.Should().Be(h.Binding.LedgerId);
        journal.Lines.Should().HaveCount(2).And.OnlyContain(x => x.Amount > 0);
        journal.Lines.Should().ContainSingle(x => x.LedgerAccountId == h.Binding.ClearingAccountId && x.Direction == JournalDirections.Debit && x.Amount == 80);
        journal.Lines.Should().ContainSingle(x => x.LedgerAccountId == revenueId && x.Direction == JournalDirections.Credit && x.Amount == 80);
        var clearing = await h.Db.JournalEntryLines.Where(x => x.LedgerAccountId == h.Binding.ClearingAccountId).ToListAsync();
        clearing.Sum(x => x.Direction == JournalDirections.Debit ? x.Amount : -x.Amount).Should().Be(0);
        h.Db.GiftCardOperations.Count(x => x.SourceId == intent.Id && x.Kind == "Redeem").Should().Be(1);
        h.Db.Payments.Where(x => x.PaymentIntentId == intent.Id).Sum(x => x.Amount).Should().Be(80);
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("Captured")]
    public async Task InvoiceSettlement_Should_RejectGiftAttemptWithoutCompletedFunding(string status)
    {
        using var h = await Harness.CreateAsync();
        await h.AddInvoiceAccountsAsync();
        var issued = await h.IssueAsync(100);
        var (intent, instruction) = await h.TenderAsync(issued.Code, 80, 60);
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        intent.Status = status;
        await h.Db.SaveChangesAsync();
        var invoice = new Invoice { TenantId = h.TenantId, OrderId = intent.OrderId, Total = 80, Currency = "GBP" };

        await new LedgerPostingService(h.Db).Invoking(x => x.PostInvoiceSettlementAsync(invoice))
            .Should().ThrowAsync<InvalidStateException>();

        h.Db.JournalEntries.Any(x => x.SourceType == "InvoiceSettlement").Should().BeFalse();
        h.Db.GiftCardOperations.Any(x => x.SourceId == intent.Id).Should().BeFalse();
    }

    [Theory]
    [InlineData("gift-receipt")]
    [InlineData("gift-journal")]
    [InlineData("cash-receipt")]
    [InlineData("cash-journal")]
    public async Task InvoiceSettlement_Should_RequireActualReceiptsAndJournalProof_NotJustCompletedStatus(string damagedEvidence)
    {
        using var h = await Harness.CreateAsync();
        await h.AddInvoiceAccountsAsync();
        var issued = await h.IssueAsync(100);
        var (intent, instruction) = await h.TenderAsync(issued.Code, 80, 60);
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        await h.RecordCashAsync(intent, 20);
        intent.Status = "Captured";
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();
        if (damagedEvidence.EndsWith("receipt", StringComparison.Ordinal))
        {
            var provider = damagedEvidence.StartsWith("gift", StringComparison.Ordinal) ? "GiftCard" : "Stripe";
            h.Db.Payments.Remove(await h.Db.Payments.SingleAsync(x => x.PaymentIntentId == intent.Id && x.Provider == provider));
        }
        else
        {
            var source = damagedEvidence.StartsWith("gift", StringComparison.Ordinal) ? "GiftCardRedeem" : "PaymentCapture";
            var journalId = await h.Db.JournalEntries.Where(x => x.SourceId == intent.Id && x.SourceType == source).Select(x => x.Id).SingleAsync();
            var debit = await h.Db.JournalEntryLines.SingleAsync(x => x.JournalEntryId == journalId && x.Direction == JournalDirections.Debit);
            debit.LedgerAccountId = h.Binding.ClearingAccountId;
        }
        await h.Db.SaveChangesAsync();
        var invoice = new Invoice { TenantId = h.TenantId, OrderId = intent.OrderId, Total = 80, Currency = "GBP" };

        await new LedgerPostingService(h.Db).Invoking(x => x.PostInvoiceSettlementAsync(invoice))
            .Should().ThrowAsync<InvalidStateException>();

        h.Db.JournalEntries.Any(x => x.SourceType == "InvoiceSettlement").Should().BeFalse();
    }

    [Theory]
    [InlineData(50)]
    [InlineData(60)]
    public async Task InvoiceSettlement_Should_ExcludeIssuedFaceValueFromRevenue(decimal total)
    {
        using var h = await Harness.CreateAsync();
        var revenueId = await h.AddInvoiceAccountsAsync();
        var (intent, instruction) = await h.PurchaseAsync(50, total);
        (await h.Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
        await h.Db.SaveChangesAsync();
        await h.RecordCashAsync(intent, total);
        intent.Status = "Captured";
        await h.Service.CompleteTrackedAsync(intent);
        await h.Db.SaveChangesAsync();
        var invoice = new Invoice { TenantId = h.TenantId, OrderId = intent.OrderId, Total = total, Currency = "GBP" };

        await new LedgerPostingService(h.Db).PostInvoiceSettlementAsync(invoice);

        var journal = await h.Db.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.SourceType == "InvoiceSettlement");
        journal.LedgerId.Should().Be(h.Binding.LedgerId);
        journal.Lines.Should().OnlyContain(x => x.Amount > 0);
        journal.Lines.Where(x => x.LedgerAccountId == revenueId).Sum(x => x.Amount).Should().Be(total - 50);
        journal.Lines.Should().ContainSingle(x => x.LedgerAccountId == h.Binding.ClearingAccountId && x.Direction == JournalDirections.Credit && x.Amount == 50);
        journal.Lines.Should().NotContain(x => x.LedgerAccountId == h.Binding.LiabilityAccountId);
    }

    private sealed class Harness : IDisposable
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid PartyId { get; } = Guid.NewGuid();
        public TestClock Clock { get; } = new();
        public FinanceDbContext Db { get; }
        public Mock<ITenantSettingStore> Settings { get; } = new();
        public GiftCardLedgerBinding Binding { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        public GiftCardPolicy Policy => new(true, "v1", "GBP", Binding, new(true), "terms-v1", GiftCardSettings.Proportional);
        public IDataProtectionProvider Protection { get; } = new EphemeralDataProtectionProvider();
        public GiftCardService Service { get; }
        public IJournalWriter Journals { get; }
        public string? Raw { get; set; }
        private readonly DbContextOptions<FinanceDbContext> _options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"GiftCards_{Guid.NewGuid()}").Options;

        private Harness()
        {
            var tenant = new TestTenantProvider(TenantId);
            Db = new(_options, tenant, null, Clock);
            Journals = new JournalWriter(Db, tenant, Clock);
            Settings.Setup(x => x.GetTenantValueAsync(GiftCardSettings.Policy, TenantId, It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(Raw));
            Service = new(Db, tenant, Clock, Settings.Object, Journals, Protection);
        }
        public static async Task<Harness> CreateAsync()
        {
            var h = new Harness();
            h.Db.Parties.Add(new PartyReadModel { Id = h.PartyId, TenantId = h.TenantId, DisplayName = "Buyer", Status = "Active" });
            h.Db.Ledgers.Add(new LedgerEntity { Id = h.Binding.LedgerId, TenantId = h.TenantId, BaseCurrency = "GBP" });
            h.Db.LedgerAccounts.AddRange(
                new LedgerAccount { Id = h.Binding.CashAccountId, TenantId = h.TenantId, LedgerId = h.Binding.LedgerId, Code = "gift-cash", AccountType = "Asset" },
                new LedgerAccount { Id = h.Binding.ClearingAccountId, TenantId = h.TenantId, LedgerId = h.Binding.LedgerId, Code = "gift-clearing", AccountType = "Liability" },
                new LedgerAccount { Id = h.Binding.LiabilityAccountId, TenantId = h.TenantId, LedgerId = h.Binding.LedgerId, Code = "gift-liability", AccountType = "Liability" });
            await h.Db.SaveChangesAsync();
            h.Configure(h.Policy);
            return h;
        }
        public void Configure(GiftCardPolicy policy) => Raw = JsonSerializer.Serialize(policy, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        public async Task<Guid> AddInvoiceAccountsAsync()
        {
            var otherLedger = new LedgerEntity { TenantId = TenantId, BaseCurrency = "GBP" };
            var revenue = new LedgerAccount { TenantId = TenantId, LedgerId = Binding.LedgerId, Code = "4000", AccountType = "Revenue" };
            Db.Ledgers.Add(otherLedger);
            Db.LedgerAccounts.AddRange(revenue,
                new LedgerAccount { TenantId = TenantId, LedgerId = otherLedger.Id, Code = "1000", AccountType = "Asset" },
                new LedgerAccount { TenantId = TenantId, LedgerId = otherLedger.Id, Code = "2100", AccountType = "Liability" },
                new LedgerAccount { TenantId = TenantId, LedgerId = otherLedger.Id, Code = "4000", AccountType = "Revenue" });
            await Db.SaveChangesAsync();
            return revenue.Id;
        }
        public OtherTenantScope OtherTenant() => new(_options, Clock);
        public async Task<(PaymentIntent Intent, GiftCardCheckout Instruction)> PurchaseAsync(decimal face, decimal? total = null)
        {
            var (intent, item) = await CreateOrderAsync(total ?? face, "GiftCardValue", face);
            var policy = await Service.GetPolicyAsync();
            var instruction = new GiftCardCheckout(Guid.NewGuid(), policy.Version, policy.Ledger!, policy.Validity!, policy.TermsVersion!, policy.FundingAllocation!,
                new(0, item.Id, face));
            return (intent, instruction);
        }
        public async Task<GiftCardFulfilmentSecret> IssueAsync(decimal amount)
        {
            var (intent, instruction) = await PurchaseAsync(amount);
            (await Service.PrepareTrackedAsync(intent, instruction)).Should().BeNull();
            await Db.SaveChangesAsync();
            await RecordCashAsync(intent, amount);
            intent.Status = "Captured";
            await Service.CompleteTrackedAsync(intent);
            await Db.SaveChangesAsync();
            return await Service.GetFulfilmentSecretAsync(Source(intent, instruction));
        }
        public async Task<(PaymentIntent Intent, GiftCardCheckout Instruction)> TenderAsync(string code, decimal total, decimal requested)
        {
            var (intent, item) = await CreateOrderAsync(total, "ProductPurchase", total);
            var cart = Guid.NewGuid();
            var authorization = await Service.AuthorizeForCartAsync(code, cart);
            var quote = await Service.QuoteAsync(new(cart, authorization.PrivateCartGrant!, "GBP", total, 0, requested,
                [new(0, item.Id, item.ItemType, total, 0, 0)]));
            quote.Checkout.Should().NotBeNull();
            return (intent, quote.Checkout!);
        }
        private async Task<(PaymentIntent Intent, OrderItem Item)> CreateOrderAsync(decimal amount, string type, decimal lineAmount)
        {
            var order = new Order { TenantId = TenantId, PayerPartyId = PartyId, OrderType = "ProductPurchase", CurrencyIn = "GBP", AmountIn = amount };
            var item = new OrderItem { TenantId = TenantId, OrderId = order.Id, ItemIndex = 0, ItemType = type,
                AmountIn = lineAmount, CurrencyIn = "GBP", Quantity = 1, UnitPrice = lineAmount };
            var intent = new PaymentIntent { TenantId = TenantId, OrderId = order.Id, PayerPartyId = PartyId, Currency = "GBP", Amount = amount,
                Status = "Pending", ProviderCode = "Stripe", ConnectorId = Guid.NewGuid(), ProviderPaymentIntentReference = "pi_" + Guid.NewGuid().ToString("N") };
            Db.Orders.Add(order);
            Db.OrderItems.Add(item);
            Db.PaymentIntents.Add(intent);
            await Db.SaveChangesAsync();
            return (intent, item);
        }
        public async Task RecordCashAsync(PaymentIntent intent, decimal amount)
        {
            await Journals.PostAsync(new(Binding.LedgerId, "PaymentCapture", intent.Id,
                [new("gift-cash", JournalDirections.Debit, amount, "GBP"), new("gift-clearing", JournalDirections.Credit, amount, "GBP")], Clock.UtcNow));
            Db.Payments.Add(new Payment { TenantId = TenantId, PaymentIntentId = intent.Id, Provider = "Stripe", ConnectorId = intent.ConnectorId,
                ProviderReference = intent.ProviderPaymentIntentReference, Currency = "GBP", Amount = amount, CapturedAt = Clock.UtcNow, OutcomeStatus = "Captured" });
            await Db.SaveChangesAsync();
        }
        public static GiftCardPurchaseSource Source(PaymentIntent intent, GiftCardCheckout instruction) => new(instruction.CartId,
            intent.OrderId, intent.Id, instruction.Purchase!.OrderItemId, instruction.Purchase.ItemIndex);
        public void Dispose() => Db.Dispose();
    }
    private sealed class OtherTenantScope : IDisposable
    {
        public TestTenantProvider Tenant { get; } = new(Guid.NewGuid());
        public FinanceDbContext Db { get; }
        public OtherTenantScope(DbContextOptions<FinanceDbContext> options, IClock clock) => Db = new(options, Tenant, null, clock);
        public void Dispose() => Db.Dispose();
    }
    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    }
}
