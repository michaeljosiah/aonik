using Aonik.Application.Services.Ai;
using Aonik.Application.Services.Ai.Workflows;
using Aonik.Application.Services.Billing;
using Aonik.Application.Services.Catalog;
using Aonik.Application.Services.Ledger;
using Aonik.Application.Services.Onboarding;
using Aonik.Application.Services.Payments;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IAiInsightsService, AiInsightsService>();
        services.AddScoped<IOnboardingPolicyEvaluator, OnboardingPolicyEvaluator>();

        // AI Workflows
        services.AddScoped<InvoiceInsightWorkflow>();

        return services;
    }
}
