using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Entities.Ledger;
using Aonik.Finance.Persistence;
using Aonik.Finance.Services.Payments;
using Aonik.Finance.Services.Ledger;
using Aonik.Finance.Services.Loyalty;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Payments;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Payments;

internal sealed class CheckoutPaymentTestHarness : IDisposable
{
    private readonly bool _ownsContext;
    private readonly ServiceProvider _services;
    public IServiceScopeFactory ScopeFactory => _services.GetRequiredService<IServiceScopeFactory>();
    public Mock<ITenantSettingStore> Settings { get; } = new();
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
        var services = new ServiceCollection();
        services.AddScoped<Tenant>();
        services.AddScoped<ITenantProvider>(p => p.GetRequiredService<Tenant>());
        services.AddScoped<ITenantContext>(p => p.GetRequiredService<Tenant>());
        services.AddSingleton(Clock.Object);
        services.AddSingleton(Settings.Object);
        var options = (DbContextOptions<FinanceDbContext>)Db.GetService<IDbContextOptions>();
        services.AddScoped(p => new FinanceDbContext(options, p.GetRequiredService<ITenantProvider>(), null, Clock.Object));
        services.AddScoped<IJournalWriter, JournalWriter>();
        services.AddScoped<LoyaltyService>();
        _services = services.BuildServiceProvider();
        Service = new CheckoutPaymentService(Db, tenant, Connectors.Object, [Gateway], Reconciler.Object,
            Clock.Object, NullLogger<CheckoutPaymentService>.Instance, ScopeFactory);
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

    public AsyncServiceScope NewScope()
    {
        var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = TenantId;
        return scope;
    }

    public async Task<LoyaltyCheckout> SeedLoyaltyAsync(long redeemedPoints = 100, long balance = 100)
    {
        var ledger = new Aonik.Finance.Entities.Ledger.Ledger { TenantId = TenantId, BaseCurrency = "GBP", IsCanonical = true };
        var liability = new LedgerAccount { TenantId = TenantId, LedgerId = ledger.Id, Code = "2201", AccountType = "Liability", Name = "Rewards" };
        var expense = new LedgerAccount { TenantId = TenantId, LedgerId = ledger.Id, Code = "6201", AccountType = "Expense", Name = "Rewards expense" };
        Db.Ledgers.Add(ledger);
        Db.LedgerAccounts.AddRange(liability, expense);
        await Db.SaveChangesAsync();
        var binding = new LoyaltyLedgerBinding(ledger.Id, liability.Id, expense.Id, expense.Id);
        var policy = new LoyaltyPolicy(true, "test-policy", binding);
        Settings.Setup(s => s.GetTenantValueAsync(LoyaltySettings.Policy, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(policy, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await using var scope = NewScope();
        var effective = await scope.ServiceProvider.GetRequiredService<LoyaltyService>().GetPolicyAsync();
        if (balance != 0)
            await scope.ServiceProvider.GetRequiredService<LoyaltyService>()
                .AdjustAsync(new(PayerId, Guid.NewGuid(), balance, "Fixture opening points"));
        var benefit = redeemedPoints / 100m;
        var item = new OrderItem { TenantId = TenantId, OrderId = OrderId, ItemIndex = 0, ItemType = "ProductPurchase",
            ProductId = Guid.NewGuid(), AmountIn = 42.50m + benefit, CurrencyIn = "GBP", CurrencyOut = "GBP", DetailsJson = "{}" };
        Db.OrderItems.Add(item);
        await Db.SaveChangesAsync();
        return new(Guid.NewGuid(), PayerId, false, effective.Version, binding, redeemedPoints, 85, benefit,
            42.50m + benefit, [new(0, item.Id, "ProductPurchase", item.ProductId, 42.50m + benefit,
                0, benefit, 0, 42.50m, 42.50m, 85, redeemedPoints, true, true)], PayableTotal: 42.50m);
    }

    public void Dispose()
    {
        _services.Dispose();
        if (_ownsContext) Db.Dispose();
    }

    private sealed class Tenant : ITenantContext, ITenantProvider
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
        public Guid GetCurrentTenantId() => TenantId!.Value;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = TenantId ?? Guid.Empty; return TenantId.HasValue; }
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
