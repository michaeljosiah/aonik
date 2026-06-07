using Aonik.Finance.Contracts.Models.Pricing;
using Aonik.Finance.Contracts.Models.Remittance;
using Aonik.Finance.Contracts.Services.Partners.Connectors;
using Aonik.Finance.Contracts.Services.Pricing;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Entities.Pricing;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Partners.Connectors;
using Aonik.Finance.Services.Remittance;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using LedgerEntity = Aonik.Finance.Entities.Ledger.Ledger;

namespace Aonik.Application.Tests.Remittance;

public class RemittanceOrderServiceTests
{
    private const string Signature = "test-secret";

    // The authenticated caller. Confirm/Quote require this user to be linked to the requested party via
    // a seeded UserParty row (see SeedCaller).
    private static readonly Guid CallerUserId = Guid.NewGuid();

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid id) { id = tenantId; return true; }
    }

    private sealed class TestCurrentUserProvider(Guid userId) : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => userId;
        public bool TryGetCurrentUserId(out Guid id) { id = userId; return true; }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; init; } = DateTime.UtcNow;
    }

    // Stub: confirm/get/webhook tests never call it; the quote test exercises GetRemittanceQuoteAsync,
    // which persists a quote (the service reads it back for expiry) and returns a canned response.
    private sealed class StubPricingService(FinanceDbContext db, Guid tenantId, DateTime expiresAt) : IPricingService
    {
        public Task<PricingQuoteResponse> GetBillPaymentQuoteAsync(PricingQuoteRequest r, CancellationToken ct = default)
            => throw new NotSupportedException();

        public async Task<PricingQuoteResponse> GetRemittanceQuoteAsync(PricingQuoteRequest r, CancellationToken ct = default)
        {
            var id = Guid.NewGuid();
            var fxRateId = Guid.NewGuid();
            var policyId = Guid.NewGuid();
            db.PricingQuotes.Add(new PricingQuote
            {
                Id = id,
                TenantId = tenantId,
                QuoteType = "Remittance",
                OriginCurrency = r.OriginCurrency,
                DestinationCurrency = r.DestinationCurrency,
                OriginCountry = r.OriginCountry,
                DestinationCountry = r.DestinationCountry,
                ServiceCode = r.ServiceCode,
                OriginAmount = 1000m,
                DestinationAmount = 990m,
                ExchangeRate = 1m,
                RateMarkup = 0m,
                FeesTotal = 10m,
                TotalAmount = 1010m,
                FxRateId = fxRateId,
                RateTimestamp = expiresAt.AddMinutes(-30),
                PricingPolicyId = policyId,
                PricingPolicyVersion = "v1",
                ExpiresAt = expiresAt,
                FeeBreakdownJson = "[]",
                CustomerId = r.CustomerId
            });
            await db.SaveChangesAsync(ct);

            return new PricingQuoteResponse(
                id, 1m, 0m, 10m, 1010m, 1000m, 990m, policyId, "v1", fxRateId,
                new DateTimeOffset(DateTime.SpecifyKind(expiresAt.AddMinutes(-30), DateTimeKind.Utc)),
                Array.Empty<FeeBreakdownItem>());
        }
    }

    // Records the instruction handed to the connector so a test can assert the rail it was transmitted on.
    private sealed class CapturingPayoutConnector : IPartnerPayoutConnector
    {
        public PayoutInstruction? LastInstruction { get; private set; }
        public string ProviderCode => "Simulated";
        public IReadOnlyCollection<PartnerConnectorCapability> Capabilities { get; } = new[]
        {
            new PartnerConnectorCapability(
                PartnerServiceCategory.Payout, new[] { "NG" }, new[] { "NGN" }, new[] { "Bank", "MobileMoney" })
        };

        public Task<PayoutInitiationResult> InitiatePayoutAsync(PayoutInstruction instruction, CancellationToken ct = default)
        {
            LastInstruction = instruction;
            return Task.FromResult(new PayoutInitiationResult(
                new PartnerReference(instruction.ClientReference, "pr_capture"),
                PartnerTransactionStatus.Succeeded, null, new RawProviderResponse("00", "ok", null)));
        }

        public Task<PayoutStatusResult> GetPayoutStatusAsync(PartnerReference r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<PayoutQuoteResult> QuotePayoutAsync(PayoutQuoteRequest r, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<AccountResolutionResult> ResolveAccountAsync(AccountResolutionRequest r, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private static IConfiguration WebhookConfig()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Finance:Partners:Webhooks:Simulated:SigningSecret"] = Signature
            })
            .Build();

    private static FinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new FinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static RemittanceOrderService CreateService(
        FinanceDbContext db, Guid tenantId, IClock? clock = null, IConfiguration? configuration = null,
        IPartnerPayoutConnector? payoutConnector = null, Guid? userId = null)
    {
        var simulated = new SimulatedPartnerConnector();
        var translator = new SimulatedPartnerWebhookTranslator();
        var resolver = new PartnerConnectorResolver(
            new[] { payoutConnector ?? simulated },
            new IPartnerCollectionConnector[] { simulated },
            new IPartnerBillPaymentConnector[] { simulated },
            new IPartnerWebhookTranslator[] { translator });

        var effectiveClock = clock ?? new TestClock();
        return new RemittanceOrderService(
            db,
            new StubPricingService(db, tenantId, effectiveClock.UtcNow.AddMinutes(30)),
            resolver,
            new LedgerPostingService(db),
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId ?? CallerUserId),
            configuration ?? new ConfigurationBuilder().Build(),
            effectiveClock,
            NullLogger<RemittanceOrderService>.Instance);
    }

    // Links the authenticated caller to a customer party (the UserParty bridge), so confirm/quote
    // authorization passes. Without it, the service correctly rejects the request.
    private static void SeedCaller(FinanceDbContext db, Guid tenantId, Guid partyId, Guid? userId = null)
        => db.UserParties.Add(new Aonik.Finance.Entities.UserPartyReadModel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId ?? CallerUserId,
            PartyId = partyId
        });

    private static void SeedLedger(FinanceDbContext db, Guid tenantId)
        => db.Ledgers.Add(new LedgerEntity { Id = Guid.NewGuid(), TenantId = tenantId, BaseCurrency = "NGN" });

    private static PricingQuote SeedQuote(FinanceDbContext db, Guid tenantId, Guid customerPartyId, DateTime expiresAt)
    {
        var quote = new PricingQuote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            QuoteType = "Remittance",
            OriginCurrency = "NGN",
            DestinationCurrency = "NGN",
            OriginCountry = "NG",
            DestinationCountry = "NG",
            ServiceCode = "REMITTANCE.PAYOUT",
            OriginAmount = 1000m,
            DestinationAmount = 990m,
            ExchangeRate = 1m,
            RateMarkup = 0m,
            FeesTotal = 10m,
            TotalAmount = 1010m,
            FxRateId = Guid.NewGuid(),
            RateTimestamp = expiresAt.AddMinutes(-30),
            PricingPolicyId = Guid.NewGuid(),
            PricingPolicyVersion = "v1",
            ExpiresAt = expiresAt,
            FeeBreakdownJson = "[]",
            CustomerId = customerPartyId
        };
        db.PricingQuotes.Add(quote);
        return quote;
    }

    private static ExternalPayoutAccount SeedAccount(FinanceDbContext db, Guid tenantId, Guid customerPartyId)
    {
        var account = new ExternalPayoutAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerPartyId = customerPartyId,
            BeneficiaryPartyId = Guid.NewGuid(),
            DestinationType = "Bank",
            BankCode = "058",
            MaskedAccountIdentifier = "****1234",
            AccountName = "Jane Doe",
            Currency = "NGN",
            IsVerified = true,
            ProviderBeneficiaryId = "ben_123"
        };
        db.ExternalPayoutAccounts.Add(account);
        return account;
    }

    // ── Confirm ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmAsync_Should_CreateOrderPayoutTransmission_AndComplete_When_ConnectorSucceeds()
    {
        var tenantId = Guid.NewGuid();
        var customerPartyId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var quote = SeedQuote(db, tenantId, customerPartyId, DateTime.UtcNow.AddMinutes(30));
        var account = SeedAccount(db, tenantId, customerPartyId);
        SeedCaller(db, tenantId, customerPartyId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = new ConfirmRemittanceRequest(quote.Id, customerPartyId, account.Id, "FamilySupport", "June support", null, null);

        var result = await service.ConfirmAsync(request, "idem-key-1");

        result.Status.Should().Be(OrderStatuses.Complete);
        result.PayoutId.Should().NotBeNull();
        result.ProviderReference.Should().NotBeNullOrEmpty();

        (await db.Orders.CountAsync(o => o.OrderType == "Remittance")).Should().Be(1);
        (await db.Payouts.CountAsync()).Should().Be(1);
        (await db.Transmissions.CountAsync()).Should().Be(1);
        (await db.OrderFulfilmentRefs.CountAsync()).Should().Be(1);
        (await db.Payouts.SingleAsync()).Status.Should().Be("Succeeded");
    }

    [Fact]
    public async Task ConfirmAsync_Should_PostDebitAndSettlement_When_Successful()
    {
        var tenantId = Guid.NewGuid();
        var customerPartyId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var quote = SeedQuote(db, tenantId, customerPartyId, DateTime.UtcNow.AddMinutes(30));
        var account = SeedAccount(db, tenantId, customerPartyId);
        SeedCaller(db, tenantId, customerPartyId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = new ConfirmRemittanceRequest(quote.Id, customerPartyId, account.Id, "FamilySupport", null, null, null);

        var result = await service.ConfirmAsync(request, "idem-key-2");

        (await db.JournalEntries.CountAsync(j => j.SourceType == "RemittanceDebit")).Should().Be(1);
        (await db.JournalEntries.CountAsync(j => j.SourceType == "RemittanceSettlement")).Should().Be(1);
        var debit = await db.JournalEntries.Include(j => j.Lines).SingleAsync(j => j.SourceType == "RemittanceDebit");
        debit.Lines.Should().HaveCount(2);
        debit.Lines.Sum(l => l.Direction == "Debit" ? l.Amount : -l.Amount).Should().Be(0m); // balanced
        debit.Lines.Should().OnlyContain(l => l.Currency == "NGN"); // single-currency
    }

    [Fact]
    public async Task ConfirmAsync_Should_BeIdempotent_When_SameKeyReplayed()
    {
        var tenantId = Guid.NewGuid();
        var customerPartyId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var quote = SeedQuote(db, tenantId, customerPartyId, DateTime.UtcNow.AddMinutes(30));
        var account = SeedAccount(db, tenantId, customerPartyId);
        SeedCaller(db, tenantId, customerPartyId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = new ConfirmRemittanceRequest(quote.Id, customerPartyId, account.Id, "FamilySupport", null, null, null);

        var first = await service.ConfirmAsync(request, "idem-key-3");
        var second = await service.ConfirmAsync(request, "idem-key-3");

        second.OrderId.Should().Be(first.OrderId);
        (await db.Orders.CountAsync(o => o.OrderType == "Remittance")).Should().Be(1);
        (await db.Payouts.CountAsync()).Should().Be(1);
        (await db.Transmissions.CountAsync()).Should().Be(1);
        (await db.JournalEntries.CountAsync(j => j.SourceType == "RemittanceDebit")).Should().Be(1);
        (await db.JournalEntries.CountAsync(j => j.SourceType == "RemittanceSettlement")).Should().Be(1);
    }

    [Fact]
    public async Task ConfirmAsync_Should_FailWithoutTransmission_When_DebitCannotPost()
    {
        var tenantId = Guid.NewGuid();
        var customerPartyId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        // No ledger seeded → the debit cannot post, so the connector must not be called.
        var quote = SeedQuote(db, tenantId, customerPartyId, DateTime.UtcNow.AddMinutes(30));
        var account = SeedAccount(db, tenantId, customerPartyId);
        SeedCaller(db, tenantId, customerPartyId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = new ConfirmRemittanceRequest(quote.Id, customerPartyId, account.Id, "FamilySupport", null, null, null);

        var act = async () => await service.ConfirmAsync(request, "idem-key-4");

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await db.Transmissions.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.Orders.SingleAsync(o => o.OrderType == "Remittance")).Status.Should().Be(OrderStatuses.Failed);
        (await db.Payouts.SingleAsync()).Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ConfirmAsync_Should_Reject_When_QuoteExpired()
    {
        var tenantId = Guid.NewGuid();
        var customerPartyId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var quote = SeedQuote(db, tenantId, customerPartyId, DateTime.UtcNow.AddMinutes(-5)); // expired
        var account = SeedAccount(db, tenantId, customerPartyId);
        SeedCaller(db, tenantId, customerPartyId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = new ConfirmRemittanceRequest(quote.Id, customerPartyId, account.Id, "FamilySupport", null, null, null);

        var act = async () => await service.ConfirmAsync(request, "idem-key-5");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*expired*");
        (await db.Orders.CountAsync(o => o.OrderType == "Remittance")).Should().Be(0);
    }

    [Fact]
    public async Task ConfirmAsync_Should_Reject_When_DestinationNotOwnedByCustomer()
    {
        var tenantId = Guid.NewGuid();
        var customerPartyId = Guid.NewGuid();
        var otherCustomer = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var quote = SeedQuote(db, tenantId, customerPartyId, DateTime.UtcNow.AddMinutes(30));
        var account = SeedAccount(db, tenantId, otherCustomer); // owned by someone else
        SeedCaller(db, tenantId, customerPartyId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = new ConfirmRemittanceRequest(quote.Id, customerPartyId, account.Id, "FamilySupport", null, null, null);

        var act = async () => await service.ConfirmAsync(request, "idem-key-6");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not owned*");
    }

    [Fact]
    public async Task ConfirmAsync_Should_Reject_When_CallerDoesNotOwnCustomerParty()
    {
        // The destination account legitimately belongs to the victim party (so the account-ownership
        // check passes), but the authenticated caller is linked to a DIFFERENT party — confirming on
        // another customer's behalf must be refused before any debit/dispatch.
        var tenantId = Guid.NewGuid();
        var victimPartyId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var quote = SeedQuote(db, tenantId, victimPartyId, DateTime.UtcNow.AddMinutes(30));
        var account = SeedAccount(db, tenantId, victimPartyId);
        SeedCaller(db, tenantId, Guid.NewGuid()); // caller is linked to some other party, not the victim
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = new ConfirmRemittanceRequest(quote.Id, victimPartyId, account.Id, "FamilySupport", null, null, null);

        var act = async () => await service.ConfirmAsync(request, "idem-authz-1");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await db.Orders.CountAsync(o => o.OrderType == "Remittance")).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0); // no debit posted
    }

    // ── Get ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_Should_ReturnOrder_When_Exists()
    {
        var tenantId = Guid.NewGuid();
        var customerPartyId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var quote = SeedQuote(db, tenantId, customerPartyId, DateTime.UtcNow.AddMinutes(30));
        var account = SeedAccount(db, tenantId, customerPartyId);
        SeedCaller(db, tenantId, customerPartyId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var confirmed = await service.ConfirmAsync(
            new ConfirmRemittanceRequest(quote.Id, customerPartyId, account.Id, "FamilySupport", null, null, null), "idem-key-7");

        var fetched = await service.GetAsync(confirmed.OrderId);

        fetched.Should().NotBeNull();
        fetched!.OrderId.Should().Be(confirmed.OrderId);
        fetched.DestinationType.Should().Be("Bank");
        fetched.MaskedAccountIdentifier.Should().Be("****1234");
    }

    [Fact]
    public async Task GetAsync_Should_ReturnNull_When_NotFound()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var service = CreateService(db, tenantId);

        (await service.GetAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Should_ReturnNull_When_OrderOwnedByAnotherCustomer()
    {
        // A remittance exists in the tenant but belongs to another customer; the caller (linked to a
        // different party) must not be able to read it by id — it reads as 404, not someone else's data.
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        var (order, _) = SeedTransmittedRemittance(db, tenantId); // PayerPartyId is some other party
        SeedCaller(db, tenantId, Guid.NewGuid()); // caller linked to a different party
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);

        (await service.GetAsync(order.Id)).Should().BeNull();
    }

    // ── Quote ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task QuoteAsync_Should_PersistRemittanceQuote()
    {
        var tenantId = Guid.NewGuid();
        var customerPartyId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedCaller(db, tenantId, customerPartyId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = new RemittanceQuoteRequest(customerPartyId, "NG", "NG", "NGN", "NGN", 1000m, null, null, "FamilySupport", null);

        var result = await service.QuoteAsync(request);

        result.QuoteType.Should().Be("Remittance");
        result.PricingQuoteId.Should().NotBeEmpty();
        (await db.PricingQuotes.CountAsync(q => q.QuoteType == "Remittance")).Should().Be(1);
        result.SupportedDestinationMethods.Should().Contain(m => m.DestinationType == "Bank");
    }

    // ── Webhook settlement ─────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessWebhookAsync_Should_SettleOnce_When_SignedSucceededPayout()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var (order, payout) = SeedTransmittedRemittance(db, tenantId);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Finance:Partners:Webhooks:Simulated:SigningSecret"] = Signature
            })
            .Build();
        var service = CreateService(db, tenantId, configuration: configuration);

        var envelope = BuildPayoutWebhook(payout.ClientReference, payout.ProviderReference, "Succeeded");
        await service.ProcessWebhookAsync(envelope);

        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatuses.Complete);
        (await db.Payouts.SingleAsync()).Status.Should().Be("Succeeded");
        (await db.JournalEntries.CountAsync(j => j.SourceType == "RemittanceSettlement")).Should().Be(1);

        // Duplicate delivery must not double-settle.
        await service.ProcessWebhookAsync(envelope);
        (await db.JournalEntries.CountAsync(j => j.SourceType == "RemittanceSettlement")).Should().Be(1);
    }

    [Fact]
    public async Task ProcessWebhookAsync_Should_NotMutate_When_SignatureInvalid()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var (order, payout) = SeedTransmittedRemittance(db, tenantId);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Finance:Partners:Webhooks:Simulated:SigningSecret"] = Signature
            })
            .Build();
        var service = CreateService(db, tenantId, configuration: configuration);

        var envelope = BuildPayoutWebhook(payout.ClientReference, payout.ProviderReference, "Succeeded", signature: "wrong-secret");
        await service.ProcessWebhookAsync(envelope);

        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatuses.Transmitted); // unchanged
        (await db.JournalEntries.CountAsync(j => j.SourceType == "RemittanceSettlement")).Should().Be(0);
    }

    [Fact]
    public async Task ProcessWebhookAsync_Should_SettleByClientReference_When_ProviderReferenceCollides()
    {
        // Two providers can independently mint the same provider reference. The settlement must follow
        // our own ClientReference, never settle whichever payout happens to share the provider reference.
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var (orderX, payoutX) = SeedTransmittedRemittance(db, tenantId, clientReference: "REM-AAA", providerReference: "shared-ref");
        var (orderY, payoutY) = SeedTransmittedRemittance(db, tenantId, clientReference: "REM-BBB", providerReference: "shared-ref");
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId, configuration: WebhookConfig());
        var envelope = BuildPayoutWebhook("REM-BBB", "shared-ref", "Succeeded");

        await service.ProcessWebhookAsync(envelope);

        (await db.Orders.FirstAsync(o => o.Id == orderY.Id)).Status.Should().Be(OrderStatuses.Complete);
        (await db.Orders.FirstAsync(o => o.Id == orderX.Id)).Status.Should().Be(OrderStatuses.Transmitted); // untouched
        (await db.JournalEntries.CountAsync(j => j.SourceType == "RemittanceSettlement" && j.SourceId == payoutY.Id)).Should().Be(1);
        (await db.JournalEntries.CountAsync(j => j.SourceType == "RemittanceSettlement" && j.SourceId == payoutX.Id)).Should().Be(0);
    }

    [Fact]
    public async Task ProcessWebhookAsync_Should_Reject_When_WebhookProviderDiffersFromRemittance()
    {
        var tenantId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        // Remittance was sent via "Flutterwave"; an inbound "Simulated" callback must not settle it.
        var (order, payout) = SeedTransmittedRemittance(
            db, tenantId, clientReference: "REM-CCC", providerReference: "pr_x", detailsJson: "{\"ProviderCode\":\"Flutterwave\"}");
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId, configuration: WebhookConfig());
        var envelope = BuildPayoutWebhook("REM-CCC", "pr_x", "Succeeded"); // arrives as provider "Simulated"

        await service.ProcessWebhookAsync(envelope);

        (await db.Orders.SingleAsync()).Status.Should().Be(OrderStatuses.Transmitted); // unchanged
        (await db.JournalEntries.CountAsync(j => j.SourceType == "RemittanceSettlement")).Should().Be(0);
    }

    // ── Destination rail ───────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmAsync_Should_TransmitOnBankRail_When_DestinationTypeIsLowercase()
    {
        var tenantId = Guid.NewGuid();
        var customerPartyId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var quote = SeedQuote(db, tenantId, customerPartyId, DateTime.UtcNow.AddMinutes(30));
        var account = SeedAccount(db, tenantId, customerPartyId);
        account.DestinationType = "bank"; // free-form lowercase, routes as Bank case-insensitively
        SeedCaller(db, tenantId, customerPartyId);
        await db.SaveChangesAsync();

        var capturing = new CapturingPayoutConnector();
        var service = CreateService(db, tenantId, payoutConnector: capturing);
        var request = new ConfirmRemittanceRequest(quote.Id, customerPartyId, account.Id, "FamilySupport", null, null, null);

        await service.ConfirmAsync(request, "idem-rail-1");

        capturing.LastInstruction.Should().NotBeNull();
        capturing.LastInstruction!.Destination.Should().BeOfType<BankAccountDestination>();
    }

    [Fact]
    public async Task ConfirmAsync_Should_Reject_When_DestinationRailUnsupported()
    {
        var tenantId = Guid.NewGuid();
        var customerPartyId = Guid.NewGuid();
        using var db = CreateDbContext(tenantId);
        SeedLedger(db, tenantId);
        var quote = SeedQuote(db, tenantId, customerPartyId, DateTime.UtcNow.AddMinutes(30));
        var account = SeedAccount(db, tenantId, customerPartyId);
        account.DestinationType = "Crypto"; // unsupported rail -> reject, never default to Wallet
        SeedCaller(db, tenantId, customerPartyId);
        await db.SaveChangesAsync();

        var service = CreateService(db, tenantId);
        var request = new ConfirmRemittanceRequest(quote.Id, customerPartyId, account.Id, "FamilySupport", null, null, null);

        var act = async () => await service.ConfirmAsync(request, "idem-rail-2");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Unsupported*");
        (await db.Orders.CountAsync(o => o.OrderType == "Remittance")).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0); // no debit posted
    }

    private static (Order Order, Payout Payout) SeedTransmittedRemittance(
        FinanceDbContext db,
        Guid tenantId,
        string? clientReference = null,
        string providerReference = "pr_seed_1",
        string detailsJson = "{}")
    {
        var orderId = Guid.NewGuid();
        var orderItemId = Guid.NewGuid();
        var clientRef = clientReference ?? $"REM-{orderId:N}";
        var order = new Order
        {
            Id = orderId,
            TenantId = tenantId,
            OrderType = "Remittance",
            Status = OrderStatuses.Transmitted,
            PayerPartyId = Guid.NewGuid(),
            OriginCountry = "NG",
            DestinationCountry = "NG",
            AmountIn = 1000m,
            CurrencyIn = "NGN",
            AmountOut = 990m,
            CurrencyOut = "NGN",
            FeesJson = "[]",
            ProvenanceJson = "{}",
            Items =
            {
                new OrderItem
                {
                    Id = orderItemId,
                    TenantId = tenantId,
                    OrderId = orderId,
                    ItemType = "RemittancePayout",
                    ItemIndex = 0,
                    Status = "Transmitted",
                    AmountIn = 1000m,
                    CurrencyIn = "NGN",
                    AmountOut = 990m,
                    CurrencyOut = "NGN",
                    FeesTotal = 10m,
                    DetailsJson = detailsJson
                }
            }
        };
        var payout = new Payout
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = 990m,
            Currency = "NGN",
            DebitCurrency = "NGN",
            Fee = 10m,
            FeeCurrency = "NGN",
            ClientReference = clientRef,
            ProviderReference = providerReference,
            DestinationType = "Bank",
            OrderItemId = orderItemId,
            Status = "Processing"
        };
        db.Orders.Add(order);
        db.Payouts.Add(payout);
        return (order, payout);
    }

    private static PartnerWebhookEnvelope BuildPayoutWebhook(
        string clientReference, string providerReference, string status, string signature = Signature)
    {
        var body =
            $"{{\"category\":\"Payout\",\"event\":\"payout.{status.ToLowerInvariant()}\"," +
            $"\"clientReference\":\"{clientReference}\",\"providerReference\":\"{providerReference}\"," +
            $"\"status\":\"{status}\",\"code\":\"00\",\"message\":\"ok\"}}";

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-simulated-signature"] = signature
        };

        return new PartnerWebhookEnvelope("Simulated", headers, body);
    }
}
