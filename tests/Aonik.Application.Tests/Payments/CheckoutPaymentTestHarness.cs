using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Payments;

internal sealed class CheckoutPaymentTestHarness : IDisposable
{
    private readonly bool _ownsContext;
    public Guid TenantId { get; }
    public Guid PayerId { get; } = Guid.NewGuid();
    public Guid OrderId { get; } = Guid.NewGuid();
    public Guid AttemptId { get; } = Guid.NewGuid();
    public DateTime Now { get; } = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    public FinanceDbContext Db { get; }
    public Mock<IClock> Clock { get; } = new();
    public Mock<IStripeConnectorResolver> Connectors { get; } = new();
    public Mock<ICheckoutPaymentReconciler> Reconciler { get; } = new();
    public FakeGateway Gateway { get; }
    public CheckoutPaymentService Service { get; }
    public StripeConnectorBinding Binding { get; }
    public CreateCommerceGuestPaymentIntentRequest Request => new(OrderId, 42.50m, "GBP", "Stripe", "Card",
        "https://shop.example/success", "https://shop.example/cancel", AttemptId, $"checkout:{AttemptId:N}");

    public CheckoutPaymentTestHarness(FinanceDbContext? db = null, Guid? tenantId = null)
    {
        TenantId = tenantId ?? Guid.NewGuid();
        Clock.SetupGet(c => c.UtcNow).Returns(Now);
        var tenant = new TestTenantProvider(TenantId);
        _ownsContext = db is null;
        Db = db ?? new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase($"CheckoutAttempt_{Guid.NewGuid()}").Options, tenant, null, Clock.Object);
        Binding = new StripeConnectorBinding
        {
            TenantId = TenantId, ConnectorId = Guid.NewGuid(), ProviderAccountId = "acct_merchant", LiveMode = false,
            ReturnOrigin = "https://shop.example", SecretKey = "sk_test_fixture", SigningSecrets = ["whsec_fixture"],
        };
        Connectors.Setup(c => c.ResolveSelectedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Binding);
        Gateway = new FakeGateway(TenantId);
        Reconciler.Setup(r => r.ApplyAsync(It.IsAny<Guid>(), It.IsAny<PaymentProviderCheckoutSnapshot>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, PaymentProviderCheckoutSnapshot snapshot, Guid? _, CancellationToken ct) =>
            {
                var intent = await Db.PaymentIntents.SingleAsync(p => p.Id == id, ct);
                intent.Status = snapshot.Status;
                intent.ProviderReference = snapshot.SessionId;
                intent.ProviderPaymentIntentReference = snapshot.ProviderPaymentIntentId;
                intent.NextActionRedirectUrl = snapshot.CheckoutUrl;
                await Db.SaveChangesAsync(ct);
                Db.Entry(intent).State = EntityState.Detached;
                return new PaymentIntentStateRef(id, intent.OrderId, intent.Amount, intent.Currency, intent.Status,
                    snapshot.CanNoLongerPay, snapshot.CheckoutUrl);
            });
        Service = new CheckoutPaymentService(Db, tenant, Connectors.Object, [Gateway], Reconciler.Object,
            Clock.Object, NullLogger<CheckoutPaymentService>.Instance);
    }

    public async Task SeedOrderAsync(bool includePayer = true)
    {
        if (includePayer)
            Db.Parties.Add(new PartyReadModel { Id = PayerId, TenantId = TenantId, DisplayName = "Guest", Status = "Active" });
        Db.Orders.Add(new Order
        {
            Id = OrderId, TenantId = TenantId, OrderType = "ProductPurchase", Status = "Draft",
            PayerPartyId = includePayer ? PayerId : null, AmountIn = 42.50m, CurrencyIn = "GBP", FeesJson = "[]", ProvenanceJson = "{}",
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        if (_ownsContext) Db.Dispose();
    }

    internal sealed class FakeGateway(Guid tenantId) : IPaymentProviderGateway
    {
        public string ProviderCode => "Stripe";
        public List<PaymentProviderIntentRequest> Requests { get; } = [];
        public Func<PaymentProviderIntentRequest, CancellationToken, Task<PaymentProviderIntentResult>>? OnCreate { get; set; }

        public Task<PaymentProviderIntentResult> CreateIntentAsync(PaymentProviderIntentRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return OnCreate is not null ? OnCreate(request, cancellationToken) : Task.FromResult(Result(request));
        }

        public PaymentProviderIntentResult Result(PaymentProviderIntentRequest request) => new("Stripe", "cs_test_attempt", "Pending", null,
            "https://checkout.stripe.com/c/pay/cs_test_attempt",
            new PaymentProviderCheckoutSnapshot(tenantId, request.PaymentIntentId, request.OrderId, request.ConnectorId!.Value,
                request.ProviderAccountId!, request.LiveMode!.Value, "cs_test_attempt", null, request.Amount, request.Currency,
                "Pending", false, "https://checkout.stripe.com/c/pay/cs_test_attempt"));

        public Task<PaymentProviderSetupIntentResult> CreateSetupIntentAsync(PaymentProviderSetupIntentRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
