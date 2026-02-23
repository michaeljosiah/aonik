using Aonik.Application.Services.Ai;
using Aonik.Application.Services.Ai.Workflows;
using Aonik.Application.Services.Catalog;
using Aonik.Application.Services.PersonalFinance;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers non-Platform, non-Finance Application services.
    /// Platform services are registered by <see cref="Aonik.Platform.PlatformModule"/>.
    /// Finance services are registered by <see cref="Aonik.Finance.FinanceModule"/>.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Catalog
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IPublicCatalogService, PublicCatalogService>();

        // Personal Finance
        services.AddScoped<IHouseholdService, HouseholdService>();

        // AI
        services.AddScoped<IAiInsightsService, AiInsightsService>();
        services.AddScoped<InvoiceInsightWorkflow>();

        return services;
    }
}
