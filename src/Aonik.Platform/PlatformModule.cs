using Aonik.Agents.Contracts.Services;
using Aonik.Platform.Agents;
using Aonik.Platform.Contracts.Services.Autonumbering;
using Aonik.Platform.Contracts.Services.Cms;
using Aonik.Platform.Contracts.Services.Compliance;
using Aonik.Platform.Contracts.Services.Customers;
using Aonik.Platform.Contracts.Services.Features;
using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Contracts.Services.Operations;
using Aonik.Platform.Contracts.Services.Seeding;
using Aonik.SharedKernel.Abstractions;
using Aonik.Platform.Contracts.Services.Onboarding;
using Aonik.Platform.Contracts.Services.Registration;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Autonumbering;
using Aonik.Platform.Services.Cms;
using Aonik.Platform.Services.Compliance;
using Aonik.Platform.Services.Customers;
using Aonik.Platform.Services.Features;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Services.Notifications;
using Aonik.Platform.Services.Onboarding;
using Aonik.Platform.Services.Operations;
using Aonik.Platform.Services.Party;
using Aonik.Platform.Services.Registration;
using Aonik.Platform.Services.Seeding;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Services.UserBrief;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Abstractions.UserBrief;
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
        // Shares the same physical database as the monolithic AonikDbContext
        // using dbo schema + module table prefixes.
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
                    ?? configuration.GetConnectionString("AonikDb")
                    ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                options.UseSqlServer(connectionString, sqlServerOptions =>
                    sqlServerOptions.EnableRetryOnFailure());
            }
        });

        // ── Options ──────────────────────────────────────────────────
        services.Configure<BootstrapOptions>(configuration.GetSection("Bootstrap"));
        services.Configure<OnboardingPolicyOptions>(configuration.GetSection("OnboardingPolicy"));
        services.Configure<VerificationOptions>(configuration.GetSection("Verification"));
        services.Configure<AzureMonitorAlertOptions>(configuration.GetSection("Operations:Alerts:AzureMonitor"));

        // ── Platform Services ────────────────────────────────────────
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();
        services.AddScoped<IBootstrapTenantProvisioner, TenantProvisioner>();
        services.AddScoped<IBootstrapService, BootstrapService>();
        services.AddScoped<IPendingTenantUserProvisioner, PendingTenantUserProvisioner>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuditLogAdminService, AuditLogAdminService>();
        services.AddScoped<IAuthProviderSettingsService, AuthProviderSettingsService>();
        services.AddScoped<IPayaboSetupProfileService, PayaboSetupProfileService>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.ITenantTextToSpeechSettingsService, TenantTextToSpeechSettingsService>();
        services.AddScoped<ITextToSpeechCredentialSettingsService, TextToSpeechCredentialSettingsService>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.ITextToSpeechCredentialResolver>(sp => sp.GetRequiredService<ITextToSpeechCredentialSettingsService>());
        services.AddScoped<IUserBriefContextDataProvider, UserBriefContextDataProvider>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserIdentityService, UserIdentityService>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IUserNotificationWriter, UserNotificationWriter>();
        services.AddScoped<INotificationDeviceService, NotificationDeviceService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddSingleton<INotificationRealtimePublisher, NotificationRealtimePublisher>();
        services.AddScoped<IOnboardingPolicyEvaluator, OnboardingPolicyEvaluator>();
        services.AddScoped<IPartyService, PartyService>();
        services.AddScoped<IComplianceService, ComplianceService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ICustomerAdminService, CustomerAdminService>();
        services.AddScoped<ICustomerDataService, CustomerDataService>();
        services.AddScoped<ITenantFeatureService, TenantFeatureService>();
        services.AddScoped<IAccessManagementService, AccessManagementService>();
        services.AddScoped<ITenantCurrencyProvider, TenantCurrencyProvider>();
        services.AddScoped<IPartyAccountService, PartyAccountService>();
        services.AddScoped<IScheduledJobAdminService, ScheduledJobAdminService>();
        services.AddScoped<IAlertAdminService, AlertAdminService>();
        services.AddScoped<IAlertIngestionService, AlertIngestionService>();
        services.AddScoped<IAlertAnalysisWorkflow, AzureMonitorAlertAnalysisWorkflow>();
        services.AddScoped<IAlertAudienceResolver, PlatformAdminAlertAudienceResolver>();
        services.AddScoped<IAlertProcessingService, AlertProcessingService>();
        services.AddSingleton<AlertProcessingQueue>();
        services.AddSingleton<IAlertProcessingQueue>(sp => sp.GetRequiredService<AlertProcessingQueue>());
        services.AddHostedService<AlertProcessingBackgroundService>();

        // ── CMS Services ─────────────────────────────────────────────
        services.AddScoped<IContentBlockService, ContentBlockService>();

        // ── Autonumbering Services ───────────────────────────────────
        services.AddScoped<IAutonumberingService, AutonumberingService>();

        // ── Seed Services ────────────────────────────────────────────
        services.AddScoped<IDemoSeedService, DemoSeedService>();
        services.AddScoped<IPermissionSeedService, PermissionSeedService>();

        // ── Global Seed Contributors (on-demand via admin endpoint) ──
        services.AddScoped<IGlobalSeedContributor, Services.Seeding.Contributors.IdentitySeedContributor>();
        services.AddScoped<IGlobalSeedContributor, Services.Seeding.Contributors.CatalogSeedContributor>();
        services.AddScoped<IGlobalSeedContributor, Services.Seeding.Contributors.SettingsSeedContributor>();
        services.AddScoped<IGlobalSeedContributor, Services.Seeding.Contributors.NotificationTemplateSeedContributor>();

        // Demo seed contributor — owns the Notifications part of the
        // Activity phase so a fresh demo install populates the bell badge.
        services.AddScoped<IDemoSeedContributor, Services.Seeding.PlatformDemoSeedContributor>();

        // ── Platform Domain Agent ────────────────────────────────────
        services.AddSingleton<IDomainAgentDescriptor, PlatformAgentDescriptor>();

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
