using Aonik.Application.Services.Ai;
using Aonik.Application.Services.Ai.Workflows;
using Aonik.Application.Services.Billing;
using Aonik.Application.Services.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IAiInsightsService, AiInsightsService>();

        // AI Workflows
        services.AddScoped<InvoiceInsightWorkflow>();

        return services;
    }
}
