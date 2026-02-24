using Aonik.Agents.Framework;
using Aonik.Platform.Agents;
using Aonik.Platform.Contracts.Services.Cms;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Customers;
using Aonik.Platform.Contracts.Services.Features;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Contracts.Services.Onboarding;
using Aonik.Platform.Contracts.Services.Registration;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Cms;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Services.Customers;
using Aonik.Platform.Services.Features;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Services.Notifications;
using Aonik.Platform.Services.Onboarding;
using Aonik.Platform.Services.Party;
using Aonik.Platform.Services.Registration;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Platform;

/// <summary>
/// Platform module registration. Owns Identity, Tenancy, Party/Profile,
/// Compliance, Notifications, and Operations domains.
/// </summary>
public sealed class PlatformModule : IModule
{
    public static string Name => "Platform";

    public static IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Register PlatformDbContext
        // Shares the same physical database as the monolithic AonikDbContext.
        // Uses the 'platform' schema for logical isolation.
        services.AddDbContext<PlatformDbContext>((sp, options) =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                var dbName = configuration.GetValue<string>("InMemoryDatabaseName")
                    ?? $"PlatformDb_{Guid.NewGuid()}";
                options.UseInMemoryDatabase(dbName);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString);
            }
        });

        // ── Options ──────────────────────────────────────────────────
        services.Configure<BootstrapOptions>(configuration.GetSection("Bootstrap"));
        services.Configure<OnboardingPolicyOptions>(configuration.GetSection("OnboardingPolicy"));
        services.Configure<VerificationOptions>(configuration.GetSection("Verification"));

        // ── Platform Services ────────────────────────────────────────
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();
        services.AddScoped<IBootstrapTenantProvisioner, TenantProvisioner>();
        services.AddScoped<IBootstrapService, BootstrapService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuthProviderSettingsService, AuthProviderSettingsService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserIdentityService, UserIdentityService>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddScoped<IOnboardingPolicyEvaluator, OnboardingPolicyEvaluator>();
        services.AddScoped<IPartyService, PartyService>();
        services.AddScoped<IComplianceService, ComplianceService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ICustomerAdminService, CustomerAdminService>();
        services.AddScoped<ITenantFeatureService, TenantFeatureService>();
        services.AddScoped<IAccessManagementService, AccessManagementService>();
        services.AddScoped<ITenantCurrencyProvider, TenantCurrencyProvider>();

        // ── CMS Services ─────────────────────────────────────────────
        services.AddScoped<IContentBlockService, ContentBlockService>();

        // ── Platform Domain Agent ────────────────────────────────────
        services.AddSingleton<AonikDomainAgent, PlatformDomainAgent>();

        return services;
    }
}

/// <summary>
/// Extension methods for registering the Platform module in the DI container.
/// </summary>
public static class PlatformModuleExtensions
{
    /// <summary>
    /// Adds the Platform module services to the DI container.
    /// Call this from the composition root (Program.cs).
    /// </summary>
    public static IServiceCollection AddPlatformModule(
        this IServiceCollection services,
        IConfiguration configuration)
        => PlatformModule.ConfigureServices(services, configuration);
}
