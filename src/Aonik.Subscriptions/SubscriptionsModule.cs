using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Modules;
using Aonik.Subscriptions.Contracts.Services;
using Aonik.Subscriptions.Persistence;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Services.Catalogue;
using Aonik.Subscriptions.Services.Subscriptions;
using Aonik.SharedKernel.Abstractions.Ledgers;
using Aonik.Subscriptions.Services.Ledger;
using Aonik.Subscriptions.Services.Purchases;
using Aonik.Subscriptions.Services.Usage;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Subscriptions;

/// <summary>
/// Composition-root registration for the Subscriptions module (Spec 087). Wires the module-scoped
/// <see cref="SubscriptionsDbContext"/> and the catalogue service. The canonical migration stream
/// stays in <c>AonikDbContext</c>; this context declares no migrations.
///
/// Spec 087 P2 registers the catalogue only. The subscription lifecycle, grants and metering
/// (<c>ISubscriptionService</c>, <c>IEntitlementReader</c>, <c>IUsageMeter</c>) land in P3–P4, and
/// the money paths in P5–P6 once Spec 088 has delivered the Finance contracts they need.
/// </summary>
public sealed class SubscriptionsModule : IModule
{
    public static string Name => "Subscriptions";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<SubscriptionsDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"SubscriptionsDb_{Guid.NewGuid()}";
                options.UseInMemoryDatabase(dbName);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? configuration.GetConnectionString("AonikDb")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString, o => o.EnableRetryOnFailure());
            }
        });

        services.AddScoped<ICatalogueService, CatalogueService>();

        // Spec 087 P3 - subscription lifecycle, the free-tier settlement path, and counter usage.
        services.AddScoped<SubscriberAuthorization>();
        services.AddScoped<ISubscriberAuthorizer, TenantSubscriberAuthorizer>();
        services.AddScoped<ISubscriberAuthorizer, PartySubscriberAuthorizer>();
        services.AddScoped<ISubscriberAuthorizer, GroupSubscriberAuthorizer>();

        // Without this the module's integration-event handlers are absent from DI, and the
        // dispatcher treats an event with zero handlers as delivered — so it marks the outbox row
        // processed and drops it. Every UsageCommittedEvent would vanish and no purchased-unit
        // revenue or provider cost would ever reach the ledger: the outbox handoff would look
        // correct and do nothing.
        //
        // ONCE. AddEventHandlersFromAssembly uses AddScoped without deduplication, so a second
        // identical scan runs every handler twice — and the dispatcher stages one inbox row per
        // invocation, so the duplicate pair violates the unique (EventId, HandlerName) index and
        // dead-letters the event instead of marking it processed.
        services.AddEventHandlersFromAssembly(typeof(SubscriptionsModule).Assembly);

        // Without this the module's integration-event handlers are absent from DI, and the
        // dispatcher treats an event with zero handlers as delivered — so it marks the outbox row
        // processed and drops it. Every UsageCommittedEvent would vanish and no purchased-unit
        // revenue or provider cost would ever reach the ledger: the outbox handoff would look
        // correct and do nothing.
        services.AddScoped<EntitlementMaterialiser>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IEntitlementReader, EntitlementReader>();
        services.AddScoped<UsageLedgerPoster>();
        services.AddScoped<IUsageMeter, UsageMeter>();

        // Spec 090 — offline-verifiable entitlement. The issuer projects IEntitlementReader; it does
        // not replace it, and every server-side caller keeps reading the database.
        services.Configure<Services.Entitlements.EntitlementTokenOptions>(
            configuration.GetSection(Services.Entitlements.EntitlementTokenOptions.SectionName));
        services.AddScoped<SharedKernel.Abstractions.Entitlements.IEntitlementKeyRing, Services.Entitlements.EntitlementKeyRing>();
        services.AddScoped<SharedKernel.Abstractions.Entitlements.IEntitlementTokenIssuer, Services.Entitlements.EntitlementTokenIssuer>();

        // Spec 087 P5 - the ledger side. The accounts are declared for Finance to create at
        // provisioning; the resolver plugs this module's order types into Finance's settlement
        // seam (Spec 088 §9) without either module referencing the other.
        services.AddScoped<ILedgerAccountContributor, SubscriptionLedgerAccountContributor>();
        services.AddScoped<ISettlementRevenueResolver, SubscriptionSettlementRevenueResolver>();

        // Spec 087 P6 - paid money. Both reach Finance only through SharedKernel contracts
        // (IOrderService, IInvoiceWriter, IRecurringPaymentInitiator), never by reference.
        services.AddScoped<IEntitlementPurchaseService, EntitlementPurchaseService>();
        services.AddScoped<EntitlementPurchaseService>();
        services.AddScoped<SubscriptionRenewalService>();
        services.AddScoped<UsageSweeper>();

        return services;
    }
}

public static class SubscriptionsModuleExtensions
{
    public static IServiceCollection AddSubscriptionsModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => SubscriptionsModule.ConfigureServices(services, configuration);
}
