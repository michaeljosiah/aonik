using Aonik.Application.Services.Ai;
using Aonik.Application.Services.Ai.Workflows;
using Aonik.Application.Services.Billing;
using Aonik.Application.Services.Catalog;
using Aonik.Application.Services.Features;
using Aonik.Application.Services.Ledger;
using Aonik.Application.Services.Onboarding;
using Aonik.Application.Services.Orders;
using Aonik.Application.Services.Payments;
using Aonik.Application.Services.Registration;
using Aonik.Application.Services.Identity;
using Aonik.Application.Services.Identity.Provisioning;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Pricing;
using Aonik.Application.Services.Settings;
using Aonik.Application.Services.Parties;
using Aonik.Application.Services.Customers;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IPublicCatalogService, PublicCatalogService>();
        services.AddScoped<IAiInsightsService, AiInsightsService>();
        services.AddScoped<IOnboardingPolicyEvaluator, OnboardingPolicyEvaluator>();

        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserIdentityService, UserIdentityService>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IAccessManagementService, AccessManagementService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantFeatureService, TenantFeatureService>();
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();
        services.AddScoped<IBootstrapTenantProvisioner, TenantProvisioner>();
        services.AddScoped<IBootstrapService, BootstrapService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuthProviderSettingsService, AuthProviderSettingsService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IPricingPolicyService, PricingPolicyService>();
        services.AddScoped<IFxRateService, FxRateService>();
        services.AddScoped<IFxQuoteService, FxQuoteService>();
        services.AddSingleton<ICurrencyMetadataProvider, CurrencyMetadataProvider>();
        services.AddScoped<IComplianceService, ComplianceService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IPartyService, PartyService>();
        services.AddScoped<ICustomerAdminService, CustomerAdminService>();
        services.AddScoped<IOrderService, OrderService>();

        // AI Workflows
        services.AddScoped<InvoiceInsightWorkflow>();

        return services;
    }
}
