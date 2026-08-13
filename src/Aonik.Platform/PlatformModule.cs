using Aonik.Platform.Agents;
using Aonik.SharedKernel.Abstractions.Agents;
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
using Aonik.Platform.Services.Seeding.Phases;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Services.UserBrief;
using Aonik.SharedKernel.Modules;
using Aonik.SharedKernel.Abstractions.UserBrief;
using Aonik.SharedKernel.Events;
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
        services.Configure<Services.Consent.ConsentOptions>(configuration.GetSection(Services.Consent.ConsentOptions.SectionName));
        services.Configure<UserLifecycleOptions>(configuration.GetSection("UserLifecycle"));
        services.Configure<AzureMonitorAlertOptions>(configuration.GetSection("Operations:Alerts:AzureMonitor"));

        // ── Platform Services ────────────────────────────────────────
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();
        services.AddScoped<IBootstrapTenantProvisioner, TenantProvisioner>();

        // Spec 065 — business-type configuration packs
        services.AddSingleton<Aonik.SharedKernel.Abstractions.Packs.IConfigPackSource, Aonik.SharedKernel.Abstractions.Packs.ConfigPackSource>();
        services.AddScoped<Aonik.Platform.Contracts.Services.Packs.IConfigPackApplier, Aonik.Platform.Services.Packs.ConfigPackApplier>();
        services.AddScoped<IBootstrapService, BootstrapService>();
        services.AddScoped<IPendingTenantUserProvisioner, PendingTenantUserProvisioner>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuditLogAdminService, AuditLogAdminService>();
        services.AddScoped<IAuthProviderSettingsService, AuthProviderSettingsService>();
        services.AddScoped<ICommunicationProviderSettingsService, CommunicationProviderSettingsService>();
        services.AddScoped<IPaymentGatewaySettingsService, PaymentGatewaySettingsService>();
        services.AddScoped<IPayaboSetupProfileService, PayaboSetupProfileService>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.ITenantTextToSpeechSettingsService, TenantTextToSpeechSettingsService>();
        // Voice provider settings — same JSON-payload-on-existing-Settings-table pattern as TTS.
        // See docs/specifications/022.aonik-voice-realtime.md Phase 2.
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.ITenantVoiceProviderSettingsService, TenantVoiceProviderSettingsService>();
        // Voice provider credentials — encrypted at rest with status-only readback,
        // resolves tenant override → host default → configuration fallback. Spec Phase 5.
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IVoiceProviderCredentialSettingsService, VoiceProviderCredentialSettingsService>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IVoiceProviderCredentialResolver>(
            sp => sp.GetRequiredService<Aonik.SharedKernel.Abstractions.Ai.IVoiceProviderCredentialSettingsService>());
        services.AddScoped<ITextToSpeechCredentialSettingsService, TextToSpeechCredentialSettingsService>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.ITextToSpeechCredentialResolver>(sp => sp.GetRequiredService<ITextToSpeechCredentialSettingsService>());
        services.AddScoped<IUserBriefContextDataProvider, UserBriefContextDataProvider>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserIdentityService, UserIdentityService>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.ICurrentPartyResolver, Services.Identity.CurrentPartyResolver>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IUserNotificationWriter, UserNotificationWriter>();
        services.AddScoped<INotificationDeviceService, NotificationDeviceService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddSingleton<NotificationRealtimePublisher>();

        // ── Task / WorkItem Scheduling (Spec 034) ────────────────────
        // The data-defined task primitive lives in Platform beside Notifications and is
        // consumed cross-module via the SharedKernel ITaskService contract. The dispatcher
        // and the keyed notify_user action handler are registered with the dispatch wiring.
        services.AddScoped<Services.Tasks.WorkItemService>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.Tasks.ITaskService>(
            sp => sp.GetRequiredService<Services.Tasks.WorkItemService>());
        services.AddScoped<Aonik.Platform.Contracts.Services.Tasks.IWorkItemAdminService>(
            sp => sp.GetRequiredService<Services.Tasks.WorkItemService>());
        services.AddScoped<Aonik.Platform.Contracts.Services.Tasks.IWorkItemDispatcher, Services.Tasks.WorkItemDispatcher>();
        services.AddSingleton<Services.Tasks.RecurrenceCalculator>();
        services.AddSingleton<Services.Tasks.ITaskActionHandlerCatalog, Services.Tasks.TaskActionHandlerCatalog>();
        // The reference low-risk action handler. Other modules register their own keyed handlers
        // (e.g. Finance → create_payment_proposal, Agents → run_agent) against the same gate.
        services.AddKeyedScoped<Aonik.SharedKernel.Abstractions.Tasks.ITaskActionHandler, Services.Tasks.NotifyUserTaskActionHandler>(
            Aonik.SharedKernel.Abstractions.Tasks.TaskActionTypes.NotifyUser);

        services.AddScoped<IOnboardingPolicyEvaluator, OnboardingPolicyEvaluator>();
        services.AddScoped<IPartyService, PartyService>();
        // ── Cross-Module Read Contracts (Spec 027 boundary) ─────────
        // Thin readers letting PersonalFinance (and other modules) read
        // Platform's Party / User aggregates via SharedKernel without
        // a direct project reference on Aonik.Platform.
        services.AddScoped<Aonik.SharedKernel.Abstractions.Platform.IPartyReader, Services.Party.PartyReader>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.Platform.IUserPartyResolver, Services.Party.UserPartyResolver>();
        services.AddScoped<Aonik.SharedKernel.Abstractions.Platform.IUserDirectoryReader, Services.Identity.UserDirectoryReader>();

        // Compliance reacts to document erasure (Spec 035 §12/§15) by marking dependent usages
        // Expired. Registering the assembly's IEventHandler implementations lets the Worker's outbox
        // dispatcher resolve them; only the Worker drains the outbox, so the handler runs there.
        services.AddEventHandlersFromAssembly(typeof(PlatformModule).Assembly);
        services.AddScoped<IComplianceService, ComplianceService>();
        services.AddScoped<IDocumentVerificationService, DocumentVerificationService>();
        services.AddScoped<ICustomerAdminService, CustomerAdminService>();
        services.AddScoped<ICustomerDataService, CustomerDataService>();
        services.AddScoped<ITenantFeatureService, TenantFeatureService>();
        services.AddScoped<IAccessManagementService, AccessManagementService>();
        services.AddScoped<IInviteAcceptanceService, InviteAcceptanceService>();
        services.AddScoped<IUserSessionBlocklist, UserSessionBlocklist>();
        services.AddScoped<ITenantCurrencyProvider, TenantCurrencyProvider>();
        services.AddScoped<IPartyAccountService, PartyAccountService>();
        services.AddScoped<IScheduledJobAdminService, ScheduledJobAdminService>();
        services.AddScoped<IAlertAdminService, AlertAdminService>();
        services.AddScoped<IAlertIngestionService, AlertIngestionService>();
        services.AddScoped<IAlertAnalysisWorkflow, AzureMonitorAlertAnalysisWorkflow>();
        services.AddScoped<PlatformAdminAlertAudienceResolver>();
        services.AddScoped<AlertProcessingService>();
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

        // ── Demo Seed Phase Helpers ───────────────────────────────────
        services.AddScoped<IdentityRoleSeedPhase>();
        services.AddScoped<PartySeedPhase>();
        services.AddScoped<CrossBorderTenantSeedPhase>();
        services.AddScoped<SeedMarkerPhase>();
        services.AddScoped<ReverseSeedPhase>();

        // ── Global Seed Contributors (on-demand via admin endpoint) ──
        services.AddScoped<IGlobalSeedContributor, Services.Seeding.Contributors.IdentitySeedContributor>();
        services.AddScoped<IGlobalSeedContributor, Services.Seeding.Contributors.CatalogSeedContributor>();
        services.AddScoped<IGlobalSeedContributor, Services.Seeding.Contributors.SettingsSeedContributor>();
        services.AddScoped<IGlobalSeedContributor, Services.Seeding.Contributors.NotificationTemplateSeedContributor>();

        // Demo seed contributor — owns the Notifications part of the
        // Activity phase so a fresh demo install populates the bell badge.
        services.AddScoped<IDemoSeedContributor, Services.Seeding.PlatformDemoSeedContributor>();

        // ── Platform Domain Agent ────────────────────────────────────
        // ── Spec 095 G2 — guardian verification ──────────────────────────────
        // One IGuardianVerifier per method; the factory picks the strongest that is both accepted in
        // the jurisdiction and actually available for the party. There is deliberately no
        // "unverified" fallback registered — consent without verification is not consent.
        services.AddScoped<SharedKernel.Abstractions.Consent.IConsentJurisdictionResolver, Services.Consent.ConsentJurisdictionResolver>();
        services.AddScoped<SharedKernel.Abstractions.Consent.IGuardianVerifier, Services.Consent.PaymentInstrumentGuardianVerifier>();
        services.AddScoped<SharedKernel.Abstractions.Consent.IGuardianVerifierFactory, Services.Consent.GuardianVerifierFactory>();
        services.AddScoped<Services.Consent.IGuardianVerificationRecorder, Services.Consent.GuardianVerificationRecorder>();

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
