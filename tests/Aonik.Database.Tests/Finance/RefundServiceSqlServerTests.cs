using System.Collections.Concurrent;
using System.Text.Json;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Partners;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.GiftCards;
using Aonik.Finance.Services.Loyalty;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Aonik.Database.Tests.Finance;

public sealed partial class CheckoutPaymentReconciliationSqlServerTests
{
    [SkippableFact]
    public async Task RefundReclaim_Should_CompeteWithGiftCheckoutForSameNativeInstrumentBudget()
    {
        RequireSql();
        var gate = new PauseRefundAdmission { IncludeGiftCheckout = true };
        var source = new RefundSource();
        await using var h = await BuildRefundAsync(source, gate, seedPayment: false);
        var setup = await SeedGiftRequestAsync(h, 10m);
        Guid cardId;
        Guid purchaseOrderId;
        RefundRequest request;
        await using (var scope = h.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var card = await db.GiftCards.SingleAsync(x => x.TenantId == h.Intent.TenantId);
            cardId = card.Id;
            purchaseOrderId = card.OrderId;
            source.Value = new(card.OrderId, card.PaymentIntentId, null, "GBP", card.FaceValue, 0m, card.FaceValue,
                [new("gift", card.OrderItemId, "GiftCardValue", "Original purchased gift value", card.FaceValue, 0m, 0, 0)]);
            var draft = new RefundDraft("Return unused purchased value", [new("gift", FullRemaining: true)]);
            var preview = await scope.ServiceProvider.GetRequiredService<RefundService>().PreviewAsync(purchaseOrderId, draft);
            request = new(Guid.NewGuid(), draft.Reason, draft.Selections, preview.Version);
        }
        gate.Armed = true;
        var refund = RequestPurchaseReturnAsync();
        var checkout = CreateGiftAsync(h, setup.Request);
        try
        {
            var reached = await Task.WhenAny(gate.Reached.Task, refund, checkout).WaitAsync(TimeSpan.FromSeconds(30));
            if (reached != gate.Reached.Task) { await reached; throw new InvalidOperationException("Gift reclaim race ended before both native claims."); }
        }
        finally { gate.Release.TrySetResult(); }
        var admittedRefund = await refund.WaitAsync(TimeSpan.FromSeconds(30));
        var checkoutResult = await checkout.WaitAsync(TimeSpan.FromSeconds(30));

        await using var verify = h.NewScope();
        var final = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var reclaim = await final.Refunds.Where(x => x.TenantId == h.Intent.TenantId && x.GiftReclaimCardId == cardId
            && x.GiftReclaimReleasedAtUtc == null && x.Status != "Failed").SumAsync(x => x.GiftReclaimAmount);
        var reserved = await final.GiftCardCheckoutAttempts.Where(x => x.TenantId == h.Intent.TenantId && x.GiftCardId == cardId
            && x.Status == "Reserved").SumAsync(x => x.ReservedAmount);
        (reclaim + reserved).Should().BeLessThanOrEqualTo(23.45m);
        if (admittedRefund)
        {
            reclaim.Should().Be(23.45m);
            reserved.Should().Be(0m);
            checkoutResult.Status.Should().Be("Cancelled");
            h.Gateway.Requests.Should().BeEmpty();
        }
        else
        {
            reclaim.Should().Be(0m);
            reserved.Should().Be(10m);
            checkoutResult.Status.Should().Be("Pending");
            h.Gateway.Requests.Should().ContainSingle();
        }
        var balance = await verify.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(setup.Code);
        balance!.Balance.Should().Be(23.45m);
        balance.Reserved.Should().Be(reclaim + reserved);
        h.Gateway.RefundRequests.Should().BeEmpty();

        async Task<bool> RequestPurchaseReturnAsync()
        {
            await using var scope = h.NewScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<RefundService>().RequestAsync(purchaseOrderId, request);
                return true;
            }
            catch (InvalidStateException) { return false; }
        }
    }

    [SkippableFact]
    public async Task Refund_Should_FindOnlyAfterKeyHorizonAndKeepUnknownBudgetWhenLookupIsEmpty()
    {
        RequireSql();
        var source = new RefundSource();
        await using var h = await BuildRefundAsync(source);
        await SeedCashRefundSourceAsync(h, source);
        var request = await PreviewRefundAsync(h, 5m);
        await RequestRefundAsync(h, request);
        h.Gateway.LoseRefundResponseOnce = true;
        var first = () => ReconcileRefundAsync(h, request.RefundId);
        await first.Should().ThrowAsync<InvalidOperationException>();
        var started = DateTime.UtcNow.AddHours(-24);
        await using (var age = h.NewScope())
        {
            var db = age.ServiceProvider.GetRequiredService<FinanceDbContext>();
            (await db.Refunds.SingleAsync(x => x.Id == request.RefundId)).ProviderRequestStartedAtUtc = started;
            await db.SaveChangesAsync();
        }
        h.Gateway.EmptyRefundLookup = true;

        await ReconcileRefundAsync(h, request.RefundId);

        h.Gateway.RefundRequests.Should().ContainSingle("an aged unknown request must never issue another provider POST");
        h.Gateway.RefundFindRequests.Should().ContainSingle().Which.RefundId.Should().Be(request.RefundId);
        await using var verify = h.NewScope();
        var final = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var row = await final.Refunds.SingleAsync(x => x.Id == request.RefundId);
        row.Status.Should().Be("NeedsReconciliation");
        row.ProviderRequestStartedAtUtc.Should().Be(started);
        row.EffectsAppliedAtUtc.Should().BeNull();
        row.ProviderReference.Should().BeNull();
        (await final.JournalEntries.CountAsync(x => x.TenantId == h.Intent.TenantId && x.SourceId == request.RefundId)).Should().Be(0);
        (await verify.ServiceProvider.GetRequiredService<RefundService>().GetAsync(h.Intent.OrderId)).CanRequest.Should().BeFalse();
    }

    [SkippableFact]
    public async Task Refund_Should_AdmitOnlyOneOperatorForLastBudgetUnderNativeIntentVersion()
    {
        RequireSql();
        var gate = new PauseRefundAdmission();
        var source = new RefundSource();
        await using var h = await BuildRefundAsync(source, gate);
        await SeedCashRefundSourceAsync(h, source);
        var first = await PreviewRefundAsync(h, source.Value!.Total);
        var second = first with { RefundId = Guid.NewGuid() };
        gate.Armed = true;
        var a = TryRequestRefundAsync(h, first);
        var b = TryRequestRefundAsync(h, second);
        try
        {
            var reached = await Task.WhenAny(gate.Reached.Task, a, b).WaitAsync(TimeSpan.FromSeconds(30));
            if (reached != gate.Reached.Task) { await reached; throw new InvalidOperationException("Refund request ended before its native claim."); }
        }
        finally { gate.Release.TrySetResult(); }

        var outcomes = await Task.WhenAll(a, b).WaitAsync(TimeSpan.FromSeconds(30));

        outcomes.Should().BeEquivalentTo([true, false]);
        await using var verify = h.NewScope();
        var db = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        (await db.Refunds.CountAsync(x => x.TenantId == h.Intent.TenantId && x.PaymentIntentId == h.Intent.Id)).Should().Be(1);
        (await db.Set<OutboxMessage>().CountAsync(x => x.TenantId == h.Intent.TenantId
            && x.EventType == typeof(RefundReconciliationRequestedEvent).FullName)).Should().Be(1);
        (await db.PaymentIntents.SingleAsync(x => x.Id == h.Intent.Id)).RowVersion.Should().HaveCount(8);
        h.Gateway.RefundRequests.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task Refund_Should_ReturnOnePennyOnceAgainstActualReceiptAndOriginalCashJournal()
    {
        RequireSql();
        var source = new RefundSource();
        await using var h = await BuildRefundAsync(source);
        await SeedCashRefundSourceAsync(h, source);
        var request = await PreviewRefundAsync(h, 0.01m);
        await RequestRefundAsync(h, request);

        await ReconcileRefundAsync(h, request.RefundId);
        await ReconcileRefundAsync(h, request.RefundId);

        await using var verify = h.NewScope();
        var db = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var refund = await db.Refunds.SingleAsync(x => x.Id == request.RefundId);
        refund.Status.Should().Be("Succeeded");
        refund.Amount.Should().Be(0.01m);
        refund.EffectsAppliedAtUtc.Should().NotBeNull();
        refund.RowVersion.Should().HaveCount(8);
        var returned = await db.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.TenantId == h.Intent.TenantId
            && x.SourceType == "RefundCash" && x.SourceId == request.RefundId);
        var captured = await db.JournalEntries.Include(x => x.Lines).SingleAsync(x => x.TenantId == h.Intent.TenantId
            && x.SourceType == "PaymentCapture" && x.SourceId == h.Intent.Id);
        returned.Lines.Should().HaveCount(2).And.OnlyContain(x => x.Amount == 0.01m);
        returned.Lines.Single(x => x.Direction == "Debit").LedgerAccountId
            .Should().Be(captured.Lines.Single(x => x.Direction == "Credit").LedgerAccountId);
        returned.Lines.Single(x => x.Direction == "Credit").LedgerAccountId
            .Should().Be(captured.Lines.Single(x => x.Direction == "Debit").LedgerAccountId);
        captured.Lines.Should().OnlyContain(x => x.Amount == 23.45m);
        (await db.PaymentIntents.SingleAsync(x => x.Id == h.Intent.Id)).Status.Should().Be("Captured");
        (await db.Orders.SingleAsync(x => x.Id == h.Intent.OrderId)).Status.Should().Be("Complete");
        (await db.Payments.SingleAsync(x => x.PaymentIntentId == h.Intent.Id)).Amount.Should().Be(23.45m);
        (await db.OrderHistoryEvents.CountAsync(x => x.OrderId == h.Intent.OrderId && x.EventType == "RefundCompleted")).Should().Be(1);
        h.Gateway.RefundRequests.Should().ContainSingle().Which.Amount.Should().Be(0.01m);
    }

    [SkippableFact]
    public async Task Refund_Should_KeepUnknownBudgetAndReplaySameAuthorizedKeyAfterLostProviderResponse()
    {
        RequireSql();
        var source = new RefundSource();
        await using var h = await BuildRefundAsync(source);
        await SeedCashRefundSourceAsync(h, source);
        var request = await PreviewRefundAsync(h, 5m);
        await RequestRefundAsync(h, request);
        h.Gateway.LoseRefundResponseOnce = true;

        var fail = () => ReconcileRefundAsync(h, request.RefundId);
        await fail.Should().ThrowAsync<InvalidOperationException>().WithMessage("*original request remains reserved*");
        await using (var check = h.NewScope())
        {
            var db = check.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var row = await db.Refunds.SingleAsync(x => x.Id == request.RefundId);
            row.Status.Should().Be("Unknown");
            row.ProviderRequestStartedAtUtc.Should().NotBeNull();
            row.EffectsAppliedAtUtc.Should().BeNull();
            var blocked = () => check.ServiceProvider.GetRequiredService<RefundService>()
                .PreviewAsync(h.Intent.OrderId, new("Second request", [new("goods", 1m)]));
            await blocked.Should().ThrowAsync<InvalidStateException>().WithMessage("*existing refund*");
        }
        (await RequestRefundAsync(h, request)).Status.Should().Be("Unknown");

        await ReconcileRefundAsync(h, request.RefundId);

        h.Gateway.RefundRequests.Should().HaveCount(2).And.OnlyContain(x => x.RefundId == request.RefundId
            && x.IdempotencyKey == $"refund:{request.RefundId:N}" && x.Amount == 5m);
        h.Gateway.RefundStates.Should().ContainSingle();
        await using var verify = h.NewScope();
        (await verify.ServiceProvider.GetRequiredService<FinanceDbContext>().JournalEntries.CountAsync(x => x.TenantId == h.Intent.TenantId
            && x.SourceId == request.RefundId && x.SourceType == "RefundCash")).Should().Be(1);
    }

    [SkippableTheory]
    [InlineData("pending")]
    [InlineData("requires_action")]
    public async Task Refund_Should_HoldPendingBudgetUntilFreshProviderSuccess(string providerStatus)
    {
        RequireSql();
        var source = new RefundSource();
        await using var h = await BuildRefundAsync(source);
        await SeedCashRefundSourceAsync(h, source);
        h.Gateway.InitialRefundStatus = providerStatus;
        var request = await PreviewRefundAsync(h, 5m);
        await RequestRefundAsync(h, request);

        await ReconcileRefundAsync(h, request.RefundId);

        await using (var check = h.NewScope())
        {
            var db = check.ServiceProvider.GetRequiredService<FinanceDbContext>();
            (await db.Refunds.SingleAsync(x => x.Id == request.RefundId)).Status.Should().Be("Pending");
            (await db.JournalEntries.CountAsync(x => x.TenantId == h.Intent.TenantId && x.SourceId == request.RefundId)).Should().Be(0);
            var context = await check.ServiceProvider.GetRequiredService<RefundService>().GetAsync(h.Intent.OrderId);
            context.CanRequest.Should().BeFalse();
            context.Status.Should().Be("RefundPending");
        }
        h.Gateway.RefundStates[request.RefundId] = h.Gateway.RefundStates[request.RefundId] with { Status = "succeeded" };
        await ReconcileRefundAsync(h, request.RefundId);
        h.Gateway.RefundRequests.Should().ContainSingle();
        await using var verify = h.NewScope();
        (await verify.ServiceProvider.GetRequiredService<FinanceDbContext>().Refunds.SingleAsync(x => x.Id == request.RefundId))
            .Status.Should().Be("Succeeded");
    }

    [SkippableTheory]
    [InlineData("pending")]
    [InlineData("requires_action")]
    public async Task Refund_Should_PreserveConfirmedFailureWhenOlderProviderObservationArrives(string providerStatus)
    {
        RequireSql();
        var source = new RefundSource();
        await using var h = await BuildRefundAsync(source);
        await SeedCashRefundSourceAsync(h, source);
        h.Gateway.InitialRefundStatus = providerStatus;
        var request = await PreviewRefundAsync(h, 5m);
        await RequestRefundAsync(h, request);
        await ReconcileRefundAsync(h, request.RefundId);
        var olderObservation = h.Gateway.RefundStates[request.RefundId];
        var eventId = Guid.NewGuid();
        await using (var scope = h.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            db.PartnerWebhookEvents.Add(new PartnerWebhookEvent
            {
                Id = eventId, TenantId = h.Intent.TenantId, ConnectorId = olderObservation.ConnectorId,
                ProviderCode = "Stripe", Category = "Refund", EventType = "refund.updated",
                ProviderEventId = "evt_" + eventId.ToString("N"), PayloadHash = eventId.ToString("N"),
                ProviderReference = olderObservation.ProviderRefundId, ClientReference = request.RefundId.ToString("N"),
                SignatureValid = true, ReceivedAt = DateTime.UtcNow, ProcessingStatus = "Received"
            });
            await db.SaveChangesAsync();
        }
        h.Gateway.RefundStates[request.RefundId] = olderObservation with { Status = "failed" };
        await ReconcileRefundAsync(h, request.RefundId);

        // A different worker can finish an earlier provider read after the failure committed.
        await using (var lateWorker = h.NewScope())
            await lateWorker.ServiceProvider.GetRequiredService<RefundService>()
                .ApplyAsync(request.RefundId, olderObservation, eventId);

        await using var verify = h.NewScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var refund = await verifyDb.Refunds.SingleAsync(x => x.Id == request.RefundId);
        refund.Status.Should().Be("Failed");
        refund.EffectsAppliedAtUtc.Should().BeNull();
        (await verifyDb.JournalEntries.CountAsync(x => x.TenantId == h.Intent.TenantId && x.SourceId == request.RefundId))
            .Should().Be(0);
        var inbox = await verifyDb.PartnerWebhookEvents.SingleAsync(x => x.Id == eventId);
        inbox.ProcessingStatus.Should().Be("Processed");
        inbox.ProcessedAt.Should().NotBeNull();
        var context = await verify.ServiceProvider.GetRequiredService<RefundService>().GetAsync(h.Intent.OrderId);
        context.CanRequest.Should().BeTrue("a proven failed refund must not re-hold the order budget");
        context.RemainingTotal.Should().Be(23.45m);
        h.Gateway.RefundRequests.Should().ContainSingle();
    }

    [SkippableFact]
    public async Task Refund_Should_BlockExternalProviderDiscrepancyBeforeAnyRefundPost()
    {
        RequireSql();
        var source = new RefundSource();
        await using var h = await BuildRefundAsync(source);
        await SeedCashRefundSourceAsync(h, source);
        var request = await PreviewRefundAsync(h, 5m);
        await RequestRefundAsync(h, request);
        h.Gateway.ExternalRefund = new("re_dashboard", 2m, "GBP", "succeeded", null);

        await ReconcileRefundAsync(h, request.RefundId);

        h.Gateway.RefundRequests.Should().BeEmpty();
        await using var verify = h.NewScope();
        var db = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var row = await db.Refunds.SingleAsync(x => x.Id == request.RefundId);
        row.Status.Should().Be("NeedsReconciliation");
        row.ProviderRequestStartedAtUtc.Should().BeNull();
        row.EffectsAppliedAtUtc.Should().BeNull();
        (await db.JournalEntries.CountAsync(x => x.TenantId == h.Intent.TenantId && x.SourceId == request.RefundId)).Should().Be(0);
    }

    [SkippableFact]
    public async Task Refund_Should_CorrectReturnedCashOnceWithoutRewritingOriginalCaptureOrIssuingAnotherRefund()
    {
        RequireSql();
        var source = new RefundSource();
        await using var h = await BuildRefundAsync(source);
        await SeedCashRefundSourceAsync(h, source);
        var request = await PreviewRefundAsync(h, 5m);
        await RequestRefundAsync(h, request);
        await ReconcileRefundAsync(h, request.RefundId);
        h.Gateway.RefundStates[request.RefundId] = h.Gateway.RefundStates[request.RefundId] with
            { Status = "failed", FailureCode = "lost_or_stolen_card", FailureBalanceTransactionId = "txn_returned" };

        await ReconcileRefundAsync(h, request.RefundId);
        await ReconcileRefundAsync(h, request.RefundId);

        await using var verify = h.NewScope();
        var db = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var row = await db.Refunds.SingleAsync(x => x.Id == request.RefundId);
        row.Status.Should().Be("NeedsReconciliation");
        row.EffectsAppliedAtUtc.Should().NotBeNull();
        var entries = await db.JournalEntries.Include(x => x.Lines).Where(x => x.TenantId == h.Intent.TenantId
            && x.SourceId == request.RefundId).ToListAsync();
        entries.Select(x => x.SourceType).Should().BeEquivalentTo(["RefundCash", "RefundCashReturned"]);
        var original = entries.Single(x => x.SourceType == "RefundCash");
        var corrected = entries.Single(x => x.SourceType == "RefundCashReturned");
        corrected.Lines.Should().HaveCount(2).And.OnlyContain(x => x.Amount == 5m);
        corrected.Lines.Single(x => x.Direction == "Debit").LedgerAccountId
            .Should().Be(original.Lines.Single(x => x.Direction == "Credit").LedgerAccountId);
        (await db.PaymentIntents.SingleAsync(x => x.Id == h.Intent.Id)).Status.Should().Be("Captured");
        (await db.Orders.SingleAsync(x => x.Id == h.Intent.OrderId)).Status.Should().Be("Complete");
        h.Gateway.RefundRequests.Should().ContainSingle();
    }

    [SkippableFact]
    public async Task Refund_Should_RollBackCashGiftAndPointsTogetherAfterProviderSuccessThenConverge()
    {
        RequireSql();
        var source = new RefundSource();
        var failure = new FailRefundEffectsSave();
        await using var h = await BuildRefundAsync(source, failure, seedPayment: false);
        var code = await SeedMixedRefundSourceAsync(h, source);
        var request = await PreviewRefundAsync(h, 23.45m);
        await RequestRefundAsync(h, request);
        failure.Armed = true;

        var fail = () => ReconcileRefundAsync(h, request.RefundId);
        await fail.Should().ThrowAsync<InvalidOperationException>().WithMessage("*original request remains reserved*");

        await using (var check = h.NewScope())
        {
            var db = check.ServiceProvider.GetRequiredService<FinanceDbContext>();
            (await db.Refunds.SingleAsync(x => x.Id == request.RefundId)).EffectsAppliedAtUtc.Should().BeNull();
            (await db.JournalEntries.CountAsync(x => x.TenantId == h.Intent.TenantId && x.SourceId == request.RefundId)).Should().Be(0);
            (await db.GiftCardOperations.CountAsync(x => x.TenantId == h.Intent.TenantId && x.SourceId == request.RefundId)).Should().Be(0);
            (await db.LoyaltyOperations.CountAsync(x => x.TenantId == h.Intent.TenantId && x.SourceId == request.RefundId)).Should().Be(0);
            (await check.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(code))!.Balance.Should().Be(13.45m);
            (await check.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(h.Intent.PayerPartyId!.Value))
                .BalancePoints.Should().Be(26);
        }

        await ReconcileRefundAsync(h, request.RefundId);
        await ReconcileRefundAsync(h, request.RefundId);

        await using var verify = h.NewScope();
        var final = verify.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var refund = await final.Refunds.SingleAsync(x => x.Id == request.RefundId);
        refund.Status.Should().Be("Succeeded");
        (await final.JournalEntries.Where(x => x.TenantId == h.Intent.TenantId && x.SourceId == request.RefundId)
            .Select(x => x.SourceType).ToListAsync()).Should().BeEquivalentTo(["RefundCash", "GiftCardRestore", "LoyaltyEarnReverse", "LoyaltyRedemptionRestore"]);
        (await verify.ServiceProvider.GetRequiredService<GiftCardService>().GetBalanceAsync(code))!.Balance.Should().Be(23.45m);
        (await verify.ServiceProvider.GetRequiredService<LoyaltyService>().GetBalanceAsync(h.Intent.PayerPartyId!.Value))
            .BalancePoints.Should().Be(100);
        (await final.PaymentIntents.SingleAsync(x => x.Id == h.Intent.Id)).Status.Should().Be("Captured");
        (await final.Payments.Where(x => x.PaymentIntentId == h.Intent.Id).SumAsync(x => x.Amount)).Should().Be(23.45m);
        h.Gateway.RefundStates.Should().ContainSingle().Which.Value.Amount.Should().Be(13.45m);
        h.Gateway.RefundRequests.Select(x => x.IdempotencyKey).Distinct().Should().ContainSingle();
    }

    private Task<Harness> BuildRefundAsync(RefundSource source, IInterceptor? interceptor = null, bool seedPayment = true)
        => BuildAsync(interceptor, seedPayment: seedPayment, configureServices: services =>
        {
            var user = Guid.NewGuid();
            services.AddSingleton(Mock.Of<ICurrentUserProvider>(x => x.GetCurrentUserId() == user));
            var permissions = new Mock<IPermissionService>();
            permissions.Setup(x => x.HasPermissionAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            services.AddSingleton(permissions.Object);
            services.AddSingleton<ICheckoutRefundSourceReader>(source);
            services.AddScoped<RefundService>();
            services.AddScoped<IRefundReconciler>(sp => sp.GetRequiredService<RefundService>());
        });

    private static async Task SeedCashRefundSourceAsync(Harness h, RefundSource source)
    {
        await using (var scope = h.NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            var item = new OrderItem { TenantId = h.Intent.TenantId, OrderId = h.Intent.OrderId, ItemIndex = 0,
                ItemType = "ProductPurchase", Quantity = 1, UnitPrice = 23.45m, AmountIn = 23.45m, CurrencyIn = "GBP" };
            db.Orders.Add(new Order { Id = h.Intent.OrderId, TenantId = h.Intent.TenantId, OrderType = "ProductPurchase",
                PayerPartyId = h.Intent.PayerPartyId, AmountIn = 23.45m, CurrencyIn = "GBP", Status = "Complete", Items = [item] });
            await db.SaveChangesAsync();
            source.Value = new(h.Intent.OrderId, h.Intent.Id, null, "GBP", 23.45m, 0m, 23.45m,
                [new("goods", item.Id, "Goods", "Original food", 23.45m, 0m, 0, 0)]);
        }
        await h.ObserveAsync("Captured");
    }

    private static async Task<string> SeedMixedRefundSourceAsync(Harness h, RefundSource source)
    {
        var setup = await SeedGiftRequestAsync(h, 10m);
        await using var scope = h.NewScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var tenant = h.Intent.TenantId;
        var ledger = await db.Ledgers.SingleAsync(x => x.TenantId == tenant);
        var liability = new LedgerAccount { TenantId = tenant, LedgerId = ledger.Id, Code = "2201", Name = "Reward liability", AccountType = "Liability" };
        var expense = new LedgerAccount { TenantId = tenant, LedgerId = ledger.Id, Code = "6201", Name = "Reward expense", AccountType = "Expense" };
        db.LedgerAccounts.AddRange(liability, expense);
        var item = await db.OrderItems.SingleAsync(x => x.OrderId == h.Intent.OrderId && x.TenantId == tenant);
        item.AmountIn = 24.45m;
        item.UnitPrice = 24.45m;
        (await db.Orders.SingleAsync(x => x.Id == h.Intent.OrderId)).AmountIn = 24.45m;
        await db.SaveChangesAsync();
        var binding = new LoyaltyLedgerBinding(ledger.Id, liability.Id, expense.Id, expense.Id);
        h.Settings.Setup(x => x.GetTenantValueAsync(LoyaltySettings.Policy, tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(new LoyaltyPolicy(true, "refund-test", binding), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var rewards = scope.ServiceProvider.GetRequiredService<LoyaltyService>();
        await rewards.AdjustAsync(new(h.Intent.PayerPartyId!.Value, Guid.NewGuid(), 100, "Fixture opening points"));
        var rewardPolicy = await rewards.GetPolicyAsync();
        var giftService = scope.ServiceProvider.GetRequiredService<GiftCardService>();
        var cartId = Guid.NewGuid();
        var grant = await giftService.AuthorizeForCartAsync(setup.Code, cartId);
        var quote = await giftService.QuoteAsync(new(cartId, grant.PrivateCartGrant!, "GBP", 23.45m, 0m,
            10m, [new(0, item.Id, "ProductPurchase", 24.45m, 0m, 1m)]));
        quote.ReasonCode.Should().BeNull();
        var points = new LoyaltyCheckout(cartId, h.Intent.PayerPartyId.Value, false, rewardPolicy.Version, binding, 100, 26, 1m, 24.45m,
            [new(0, item.Id, "ProductPurchase", Guid.NewGuid(), 24.45m, 0m, 1m, 10m, 13.45m, 13.45m, 26, 100, true, true)], 23.45m);
        await CreateGiftAsync(h, setup.Request with { GiftCard = quote.Checkout, Loyalty = points });
        await CaptureGiftAsync(h, h.Gateway.Requests.Single());
        // The source crosses Commerce's read seam; funding and all journals below remain real SQL services.
        source.Value = new(h.Intent.OrderId, h.Intent.Id, null, "GBP", 23.45m, 10m, 13.45m,
            [new("goods", item.Id, "Goods", "Original food", 23.45m, 10m, 26, 100)]);
        h.Gateway.RefundCapturedAmount = 13.45m;
        return setup.Code;
    }

    private static async Task<RefundRequest> PreviewRefundAsync(Harness h, decimal amount)
    {
        await using var scope = h.NewScope();
        var draft = new RefundDraft("Customer request", [new("goods", amount)]);
        var preview = await scope.ServiceProvider.GetRequiredService<RefundService>().PreviewAsync(h.Intent.OrderId, draft);
        return new(Guid.NewGuid(), draft.Reason, draft.Selections, preview.Version);
    }

    private static async Task<RefundDto> RequestRefundAsync(Harness h, RefundRequest request)
    {
        await using var scope = h.NewScope();
        return await scope.ServiceProvider.GetRequiredService<RefundService>().RequestAsync(h.Intent.OrderId, request);
    }

    private static async Task<bool> TryRequestRefundAsync(Harness h, RefundRequest request)
    {
        try { await RequestRefundAsync(h, request); return true; }
        catch (InvalidStateException) { return false; }
    }

    private static async Task ReconcileRefundAsync(Harness h, Guid refundId)
    {
        await using var scope = h.NewScope();
        await scope.ServiceProvider.GetRequiredService<IRefundReconciler>().ReconcileAsync(refundId);
    }

    private sealed class RefundSource : ICheckoutRefundSourceReader
    {
        public CheckoutRefundSource? Value { get; set; }
        public Task<CheckoutRefundSource?> ReadAsync(Guid orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Value?.OrderId == orderId ? Value : null);
    }

    private sealed partial class RecordingGateway
    {
        public ConcurrentQueue<PaymentProviderRefundRequest> RefundRequests { get; } = new();
        public ConcurrentQueue<PaymentProviderRefundRequest> RefundFindRequests { get; } = new();
        public ConcurrentDictionary<Guid, PaymentProviderRefundSnapshot> RefundStates { get; } = new();
        public bool LoseRefundResponseOnce { get; set; }
        public bool EmptyRefundLookup { get; set; }
        public string InitialRefundStatus { get; set; } = "succeeded";
        public decimal RefundCapturedAmount { get; set; } = 23.45m;
        public PaymentProviderRefundObservation? ExternalRefund { get; set; }

        public Task<PaymentProviderRefundBudget> GetRefundBudgetAsync(PaymentProviderRefundRequest request, CancellationToken cancellationToken = default)
        {
            var rows = RefundStates.Values.Select(x => new PaymentProviderRefundObservation(x.ProviderRefundId, x.Amount, x.Currency, x.Status, x.RefundId)).ToList();
            if (ExternalRefund is not null) rows.Add(ExternalRefund);
            return Task.FromResult(new PaymentProviderRefundBudget(RefundCapturedAmount, "GBP", rows));
        }
        public Task<PaymentProviderRefundSnapshot> CreateRefundAsync(PaymentProviderRefundRequest request, CancellationToken cancellationToken = default)
        {
            RefundRequests.Enqueue(request);
            var value = RefundStates.GetOrAdd(request.RefundId, _ => new(tenantId, request.RefundId, request.PaymentIntentId,
                request.OrderId, request.ConnectorId, request.ProviderAccountId, request.LiveMode, "re_" + request.RefundId.ToString("N"),
                request.ProviderPaymentIntentId, "ch_original", request.Amount, request.Currency, InitialRefundStatus, DateTime.UtcNow,
                BalanceTransactionId: "txn_refund"));
            if (LoseRefundResponseOnce)
            {
                LoseRefundResponseOnce = false;
                throw new HttpRequestException("Fixture lost provider response after refund creation.");
            }
            return Task.FromResult(value);
        }
        public Task<PaymentProviderRefundSnapshot> GetRefundAsync(PaymentProviderRefundRequest request, string providerRefundId, CancellationToken cancellationToken = default)
            => Task.FromResult(RefundStates.Values.Single(x => x.ProviderRefundId == providerRefundId));
        public Task<PaymentProviderRefundSnapshot?> FindRefundAsync(PaymentProviderRefundRequest request, CancellationToken cancellationToken = default)
        {
            RefundFindRequests.Enqueue(request);
            return Task.FromResult(!EmptyRefundLookup && RefundStates.TryGetValue(request.RefundId, out var value) ? value : null);
        }
    }

    private sealed class PauseRefundAdmission : SaveChangesInterceptor
    {
        private int _arrivals;
        public bool Armed { get; set; }
        public bool IncludeGiftCheckout { get; set; }
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;
            if (Armed && (context.ChangeTracker.Entries<Refund>().Any(x => x.State == EntityState.Added)
                || (IncludeGiftCheckout && context.ChangeTracker.Entries<PaymentIntent>()
                    .Any(x => x.State == EntityState.Added && x.Entity.Status == "Pending"))))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }

    private sealed class FailRefundEffectsSave : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<Refund>().Any(x => x.Entity.EffectsAppliedAtUtc is not null
                && x.State == EntityState.Modified))
            {
                Armed = false;
                throw new InvalidOperationException("Fixture final refund save failure.");
            }
            return ValueTask.FromResult(result);
        }
    }
}
