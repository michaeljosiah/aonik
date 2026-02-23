using Aonik.Application.Services.Ai;
using Aonik.Application.Services.Ai.Workflows;
using Aonik.Application.Services.Billing;
using Aonik.Application.Services.Catalog;
using Aonik.Application.Services.Ledger;
using Aonik.Application.Services.Orders;
using Aonik.Application.Services.Partners;
using Aonik.Application.Services.Payments;
using Aonik.Application.Services.PersonalFinance;
using Aonik.Application.Services.Pricing;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers non-Platform Application services.
    /// Platform services are registered by <see cref="Aonik.Platform.PlatformModule"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Ledger & Billing
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IBillingService, BillingService>();

        // Payments
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPublicPaymentService, PublicPaymentService>();
        services.AddSingleton<IPaymentProviderGateway, StripeSimulatedPaymentProviderGateway>();

        // Orders
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IPublicOrderService, PublicOrderService>();

        // Catalog
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IPublicCatalogService, PublicCatalogService>();

        // Pricing
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IPricingPolicyService, PricingPolicyService>();
        services.AddScoped<IFxRateService, FxRateService>();
        services.AddScoped<IFxQuoteService, FxQuoteService>();
        services.AddSingleton<ICurrencyMetadataProvider, CurrencyMetadataProvider>();

        // Partners
        services.AddScoped<IPartnerAdminService, PartnerAdminService>();

        // Personal Finance
        services.AddScoped<IHouseholdService, HouseholdService>();

        // AI
        services.AddScoped<IAiInsightsService, AiInsightsService>();
        services.AddScoped<InvoiceInsightWorkflow>();

        return services;
    }
}
