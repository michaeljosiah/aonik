using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Payments;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Payments;

/// <summary>
/// Spec 088 P4 acceptance: a stored mandate funds an order unattended, and a revoked one fails
/// distinctly and non-retryably.
///
/// The distinction is the point. A caller must be able to tell "this authorisation is gone, ask
/// the customer again" apart from "the bank declined this time, try later" — retrying the former
/// forever is how a subscription dies silently.
/// </summary>
public class PaymentMandateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId; return true; }
    }

    /// <summary>An interactive request: there is a signed-in human.</summary>
    private sealed class InteractiveUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId = Guid.NewGuid();
        public Guid? GetCurrentUserId() => _userId;
        public bool TryGetCurrentUserId(out Guid userId) { userId = _userId; return true; }
    }

    /// <summary>A background job: nobody is present.</summary>
    private sealed class BackgroundJobUserProvider : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => null;
        public bool TryGetCurrentUserId(out Guid userId) { userId = Guid.Empty; return false; }
    }

    private sealed class RecordingAuditLog : IAuditLogWriter
    {
        public List<string> Actions { get; } = [];

        public Task LogAsync(string action, string resourceType, Guid resourceId, Guid tenantId,
            Guid? actorId, string? correlationId, string? detailsJson = null, CancellationToken ct = default)
        {
            Actions.Add(action);
            return Task.CompletedTask;
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class StubGateway : IPaymentProviderGateway
    {
        public string ProviderCode => "stripe";
        public int CreateIntentCalls { get; private set; }

        public Task<PaymentProviderIntentResult> CreateIntentAsync(PaymentProviderIntentRequest request, CancellationToken ct = default)
        {
            CreateIntentCalls++;
            return Task.FromResult(new PaymentProviderIntentResult("stripe", $"pi_{Guid.NewGuid():N}", "Pending", null, null));
        }

        public Task<PaymentProviderSetupIntentResult> CreateSetupIntentAsync(PaymentProviderSetupIntentRequest request, CancellationToken ct = default)
            => Task.FromResult(new PaymentProviderSetupIntentResult("stripe", "seti", "secret", ["card"], "cus"));
    }

    private sealed class Harness
    {
        public FinanceDbContext Db { get; }
        public RecordingAuditLog Audit { get; } = new();
        public TestClock Clock { get; } = new();
        public StubGateway Gateway { get; } = new();

        public Harness()
        {
            Db = new FinanceDbContext(
                new DbContextOptionsBuilder<FinanceDbContext>()
                    .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                    .Options,
                new TestTenantProvider());
        }

        public IPaymentMandateService MandateService(ICurrentUserProvider? user = null)
            => new PaymentMandateService(Db, new TestTenantProvider(), user ?? new InteractiveUserProvider(), Audit, Clock);

        public IRecurringPaymentInitiator Initiator()
            => new RecurringPaymentInitiator(Db, new TestTenantProvider(), [Gateway], Clock);

        public async Task<(Guid PartyId, Guid MethodId)> SeedCardAsync(int? expiryMonth = null, int? expiryYear = null)
        {
            var partyId = Guid.NewGuid();
            var method = new PaymentMethod
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CustomerPartyId = partyId,
                Provider = "stripe",
                ProviderToken = "tok_visa",
                Type = "card",
                ExpiryMonth = expiryMonth,
                ExpiryYear = expiryYear
            };
            Db.PaymentMethods.Add(method);
            await Db.SaveChangesAsync();
            return (partyId, method.Id);
        }
    }

    // ---- creation is interactive-only -----------------------------------------------------

    [Fact]
    public async Task CreateAsync_Should_Refuse_When_ThereIsNoCurrentUser()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync();

        var act = async () => await h.MandateService(new BackgroundJobUserProvider())
            .CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));

        // A mandate records a human's consent, so it must originate where a human was present.
        // A job has no current user, which is what makes this enforceable rather than aspirational.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*interactive caller*");
    }

    [Fact]
    public async Task CreateAsync_Should_Refuse_AMethodBelongingToAnotherParty()
    {
        var h = new Harness();
        var (_, methodId) = await h.SeedCardAsync();

        var act = async () => await h.MandateService()
            .CreateAsync(new CreatePaymentMandateRequest(Guid.NewGuid(), methodId));

        // Otherwise one customer's consent would charge another's card.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*different party*");
    }

    [Fact]
    public async Task CreateAsync_Should_SupersedeThePreviousMandate_RatherThanAccumulate()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync();
        var service = h.MandateService();

        var first = await service.CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));
        var second = await service.CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));

        // "The party's mandate" must stay a single answer, and a stale instrument must not remain
        // chargeable after the customer re-authorises.
        var active = await service.GetActiveForPartyAsync(partyId);
        active!.Id.Should().Be(second.Id);

        var superseded = await service.GetAsync(first.Id);
        superseded!.Status.Should().Be(PaymentMandateStatuses.Revoked);
    }

    [Fact]
    public async Task CreateAsync_Should_DeriveExpiryFromTheCard()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync(expiryMonth: 9, expiryYear: 2027);

        var mandate = await h.MandateService().CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));

        // A card mandate cannot outlive the card; expiry is end-of-month.
        mandate.ExpiresAt.Should().Be(new DateTime(2027, 10, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task MandateTransitions_Should_BeAuditLogged()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync();
        var service = h.MandateService();

        var mandate = await service.CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));
        await service.RevokeAsync(mandate.Id, "Customer cancelled");

        // A standing permission to take someone's money must be answerable after the fact.
        h.Audit.Actions.Should().Contain("PaymentMandate.Created");
        h.Audit.Actions.Should().Contain("PaymentMandate.Revoked");
    }

    // ---- charging ---------------------------------------------------------------------------

    [Fact]
    public async Task CreateIntentForMandateAsync_Should_FundAnOrder_WithNoInteractiveInput()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync();
        var mandate = await h.MandateService().CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));
        var orderId = Guid.NewGuid();

        var reference = await h.Initiator().CreateIntentForMandateAsync(
            mandate.Id, orderId, 19.99m, "GBP", "sub:1:period:7:attempt:1");

        // The whole point: no provider, no method type, no return URL — the mandate supplies them.
        reference.PaymentIntentId.Should().NotBeEmpty();

        var intent = await h.Db.PaymentIntents.AsNoTracking().FirstAsync(p => p.Id == reference.PaymentIntentId);
        intent.OrderId.Should().Be(orderId);
        intent.PayerPartyId.Should().Be(partyId);
        intent.IdempotencyKey.Should().Be("sub:1:period:7:attempt:1");
    }

    [Fact]
    public async Task CreateIntentForMandateAsync_Should_BeIdempotent_And_NotReachTheProviderTwice()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync();
        var mandate = await h.MandateService().CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));
        var initiator = h.Initiator();
        var orderId = Guid.NewGuid();

        var first = await initiator.CreateIntentForMandateAsync(mandate.Id, orderId, 19.99m, "GBP", "key-1");
        var second = await initiator.CreateIntentForMandateAsync(mandate.Id, orderId, 19.99m, "GBP", "key-1");

        second.PaymentIntentId.Should().Be(first.PaymentIntentId);

        // Checked before the provider call, so a retry cannot charge the customer even once more.
        h.Gateway.CreateIntentCalls.Should().Be(1);
    }

    [Fact]
    public async Task CreateIntentForMandateAsync_Should_Refuse_AnEmptyIdempotencyKey()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync();
        var mandate = await h.MandateService().CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));

        var act = async () => await h.Initiator()
            .CreateIntentForMandateAsync(mandate.Id, Guid.NewGuid(), 19.99m, "GBP", "  ");

        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*idempotency key is required*");
    }

    [Fact]
    public async Task CreateIntentForMandateAsync_Should_FailNonRetryably_When_TheMandateIsRevoked()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync();
        var service = h.MandateService();
        var mandate = await service.CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));
        await service.RevokeAsync(mandate.Id, "Customer cancelled");

        var act = async () => await h.Initiator()
            .CreateIntentForMandateAsync(mandate.Id, Guid.NewGuid(), 19.99m, "GBP", "key-1");

        var thrown = await act.Should().ThrowAsync<MandateUnavailableException>();
        thrown.Which.IsRetryable.Should().BeFalse();
        thrown.Which.Reason.Should().Contain("revoked");

        // Nothing reached the provider — a withdrawn authorisation is not a decline to retry.
        h.Gateway.CreateIntentCalls.Should().Be(0);
    }

    [Fact]
    public async Task CreateIntentForMandateAsync_Should_FailNonRetryably_When_TheMandateHasExpired()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync(expiryMonth: 8, expiryYear: 2026);
        var mandate = await h.MandateService().CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));

        h.Clock.UtcNow = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

        var act = async () => await h.Initiator()
            .CreateIntentForMandateAsync(mandate.Id, Guid.NewGuid(), 19.99m, "GBP", "key-1");

        // Expiry is a fact about time; it must bite even if no sweep has rewritten the row.
        var thrown = await act.Should().ThrowAsync<MandateUnavailableException>();
        thrown.Which.IsRetryable.Should().BeFalse();
        h.Gateway.CreateIntentCalls.Should().Be(0);
    }

    [Fact]
    public async Task CreateIntentForMandateAsync_Should_FailNonRetryably_When_TheMandateIsUnknown()
    {
        var h = new Harness();

        var act = async () => await h.Initiator()
            .CreateIntentForMandateAsync(Guid.NewGuid(), Guid.NewGuid(), 19.99m, "GBP", "key-1");

        await act.Should().ThrowAsync<MandateUnavailableException>();
    }

    // ---- revocation cascade -----------------------------------------------------------------

    [Fact]
    public async Task RevokeForPaymentMethodAsync_Should_StopChargingAReplacedCard()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync();
        var service = h.MandateService();
        await service.CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));

        var revoked = await service.RevokeForPaymentMethodAsync(methodId, "Card replaced by issuer");

        // The authorisation follows the party, but it cannot outlive the instrument it names.
        revoked.Should().Be(1);
        (await service.GetActiveForPartyAsync(partyId)).Should().BeNull();
    }

    [Fact]
    public async Task RevokeAsync_Should_BeIdempotent()
    {
        var h = new Harness();
        var (partyId, methodId) = await h.SeedCardAsync();
        var service = h.MandateService();
        var mandate = await service.CreateAsync(new CreatePaymentMandateRequest(partyId, methodId));

        await service.RevokeAsync(mandate.Id, "Customer cancelled");
        var act = async () => await service.RevokeAsync(mandate.Id, "Customer cancelled again");

        // The customer wanted it gone, and it is gone.
        await act.Should().NotThrowAsync();
    }
}
