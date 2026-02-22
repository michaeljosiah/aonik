using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aonik.Application.Abstractions;
using Aonik.Application.Abstractions.Ai;
using Aonik.Application.Abstractions.Autonumbering;
using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Notifications;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Abstractions.ReferenceData;
using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Options;
using Aonik.Application.Services.Cms;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Application.Services.Identity.Provisioning;
using Aonik.Application.Services.Autonumbering;
using Aonik.Application.Services.Notifications;
using Aonik.Application.Services.Registration;
using Aonik.Application.Services.Settings;
using Aonik.Application.Services.Onboarding;
using Aonik.Application.Services.Pricing;

using Aonik.Infrastructure.Ai.Prompting;
using Aonik.Infrastructure.Ai.Providers;
using Aonik.Infrastructure.Authentication;
using Aonik.Infrastructure.Authentication.Account;
using Aonik.Infrastructure.Authentication.Configuration;
using Aonik.Infrastructure.Authentication.PasswordReset;
using Aonik.Infrastructure.Authentication.Provisioning;
using Aonik.Infrastructure.Authentication.TokenExchange;

using Aonik.Infrastructure.Authorization;
using Aonik.Infrastructure.Communication;
using Aonik.Infrastructure.Communication.Configuration;
using FluentStorage.Blobs;

using Aonik.Infrastructure.Identity;
using Aonik.Infrastructure.Settings;
using Aonik.Infrastructure.ReferenceData;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Infrastructure.Notifications;
using Aonik.Infrastructure.Observability;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Storage;
using Aonik.Infrastructure.BackgroundJobs;
using Aonik.Infrastructure.Caching;
using Aonik.Infrastructure.Time;
using Aonik.Infrastructure.Features;
using Aonik.Infrastructure.Seeding;
using Aonik.SharedKernel.Abstractions;
using Microsoft.FeatureManagement;
using ZiggyCreatures.Caching.Fusion;


namespace Aonik.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Core abstractions
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUserContext, HttpContextCurrentUserContext>();
        services.AddScoped<ICurrentUserProvider, HttpContextCurrentUserProvider>();
        services.AddScoped<ICorrelationContext, HttpContextCorrelationContext>();
        services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
        services.Configure<BootstrapOptions>(configuration.GetSection("Bootstrap"));
        services.Configure<PlatformAdminOptions>(configuration.GetSection("PlatformAdmin"));
        services.Configure<CommunicationOptions>(configuration.GetSection("Communication"));
        services.Configure<OnboardingPolicyOptions>(configuration.GetSection("OnboardingPolicy"));
        services.Configure<VerificationOptions>(configuration.GetSection("Verification"));
        services.Configure<BlobStorageOptions>(configuration.GetSection("BlobStorage"));
        services.AddFusionCache();
        services.AddDataProtection();

        services.AddSingleton<ICurrencyMetadataProvider, CurrencyMetadataProvider>();

        services.AddSingleton<ICachePolicyProvider, CachePolicyProvider>();
        services.AddSingleton<ICacheSetRegistry, CacheSetRegistry>();
        services.AddSingleton<ICacheInvalidationPublisher, CacheInvalidationPublisher>();
        services.AddSingleton<FusionCacheInvalidationHandler>();
        services.AddHostedService<CacheInvalidationSubscriptionService>();
        services.AddScoped<ICacheStore, FusionCacheStore>();
        services.AddScoped<ICacheManagementService, CacheManagementService>();

        services.AddFeatureManagement()
            .AddFeatureFilter<TenantFeatureFilter>();

        services.AddScoped<IFeatureManager, DatabaseFeatureManager>();
        services.AddScoped<Aonik.Application.Services.Seeding.IDemoSeedService, DemoSeedService>();
        services.AddScoped<Aonik.Application.Services.Seeding.IPermissionSeedService, PermissionSeedService>();

        // Blob Storage factory (shared provider, content-type aware)
        services.AddSingleton<IBlobStorageFactory, BlobStorageFactoryService>();

        // Image Processing Service
        services.AddScoped<IImageProcessingService, ImageProcessingService>();

        // Profile Photo Store abstraction
        services.AddScoped<IProfilePhotoStore, ProfilePhotoStore>();
        
        services.AddScoped<IDocumentFileStore, DocumentFileStore>();

        services.AddHostedService<ProfilePhotoStorageInitializer>();

        // Multitenancy

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantProvider, HttpContextTenantProvider>();

        // Database configuration
        // Testing environment uses InMemory database (configured in test projects)
        // All other environments use SQL Server
        if (environment.IsEnvironment("Testing"))
        {
            // InMemory database will be configured in test infrastructure (CustomWebApplicationFactory)
            // This is a no-op branch to allow tests to override DbContext
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                if (environment.IsDevelopment())
                {
                    connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                }
                else
                {
                    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for SQL Server.");
                }
            }

            services.AddDbContext<AonikDbContext>((sp, options) =>
            {
                options.UseSqlServer(connectionString);
            });
        }

        services.AddScoped<IAonikDbContext>(sp => sp.GetRequiredService<AonikDbContext>());

        // Application Services
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();
        services.AddScoped<IBootstrapTenantProvisioner, TenantProvisioner>();
        services.AddScoped<IBootstrapService, BootstrapService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<ISettingValueProtector, SettingValueProtector>();
        services.AddScoped<ISettingProvider, SettingService>();
        services.AddScoped<ISettingManager, SettingService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IAutonumberingService, AutonumberingService>();
        services.AddScoped<IAuthProviderSettingsService, AuthProviderSettingsService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserIdentityService, UserIdentityService>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IVerificationService, VerificationService>();
        services.AddScoped<IContentBlockService, ContentBlockService>();
        services.AddScoped<INotificationTemplateService, NotificationTemplateService>();
        services.AddScoped<IOnboardingPolicyEvaluator, OnboardingPolicyEvaluator>();
        services.AddHttpClient<Auth0UserProvisioner>();
        services.AddHttpClient<AzureAdUserProvisioner>();
        services.AddHttpClient<Auth0AuthTokenService>();
        services.AddHttpClient<AzureAdAuthTokenService>();
        services.AddHttpClient<Auth0PasswordResetService>();
        services.AddHttpClient<AzureAdB2cPasswordResetService>();
        services.AddHttpClient<Auth0AccountService>();
        services.AddHttpClient<AzureAdAccountService>();
        services.AddScoped<IIdpUserProvisionerFactory, IdpUserProvisionerFactory>();
        services.AddScoped<IAuthTokenServiceFactory, AuthTokenServiceFactory>();
        services.AddScoped<IIdpPasswordResetServiceFactory, IdpPasswordResetServiceFactory>();
        services.AddScoped<IIdpAccountServiceFactory, IdpAccountServiceFactory>();
        services.AddSingleton<IEmailSender, AzureCommunicationEmailSender>();
        services.AddSingleton<ISmsSender, AzureCommunicationSmsSender>();
        services.AddSingleton<INotificationTemplateRenderer, ScribanNotificationTemplateRenderer>();



        // AI
        services.AddSingleton<IPromptStore>(sp =>
        {
            var promptPath = configuration["AI:PromptTemplatesPath"];
            return new FileBasedPromptStore(promptPath);
        });

        services.AddScoped<IModelProvider, StubModelProvider>();

        // Background Jobs (Quartz)
        services.AddAonikBackgroundJobs();

        return services;
    }

    public static IServiceCollection AddAonikAuthenticationAndAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register authentication services
        services.AddScoped<ITenantResolver, TenantResolver>();
        services.AddScoped<IUserIdentityService, UserIdentityService>();
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IVerificationService, VerificationService>();

        // Add authentication
        services.AddAonikAuthentication(configuration);

        // Add authorization
        services.AddAuthorization(options =>
        {
            // Role-based policies (API boundary)
            options.AddPolicy("PlatformAdmin", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["PlatformAdmin"],
                    Array.Empty<string>())));

            options.AddPolicy("AdminPolicy", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["PlatformAdmin", "TenantAdmin"],
                    Array.Empty<string>())));

            options.AddPolicy("UserPolicy", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["PersonalUser", "Operations", "ReadOnly"],
                    Array.Empty<string>())));

            // Composite (AdminPolicy OR UserPolicy)
            options.AddPolicy("AdminUserPolicy", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["PlatformAdmin", "TenantAdmin", "PersonalUser", "Operations", "ReadOnly"],
                    Array.Empty<string>())));

            // Back-compat aliases (prefer *Policy names)
            options.AddPolicy("AdminUser", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["PlatformAdmin", "TenantAdmin", "PersonalUser", "Operations", "ReadOnly"],
                    Array.Empty<string>())));

            options.AddPolicy("Admin", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["PlatformAdmin", "TenantAdmin"],
                    Array.Empty<string>())));

            options.AddPolicy("TenantAdmin", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["TenantAdmin"],
                    Array.Empty<string>())));

            options.AddPolicy("Operations", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["Operations"],
                    Array.Empty<string>())));

            options.AddPolicy("TenantAdminOrOperations", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["TenantAdmin", "Operations"],
                    Array.Empty<string>())));

            options.AddPolicy("OperationsOrReadOnly", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["Operations", "ReadOnly"],
                    Array.Empty<string>())));

            options.AddPolicy("ReadOnly", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["ReadOnly"],
                    Array.Empty<string>())));

            options.AddPolicy("Compliance", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["Compliance"],
                    Array.Empty<string>())));

            options.AddPolicy("PersonalUser", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["PersonalUser"],
                    Array.Empty<string>())));

            options.AddPolicy("PlatformUser", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["PersonalUser", "Operations", "ReadOnly"],
                    Array.Empty<string>())));
        });

        // Register authorization handlers (SCOPED for permission handler)
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, PlatformAdminHandler>();
        services.AddScoped<IAuthorizationHandler, RoleOrPermissionAuthorizationHandler>();

        // Register dynamic policy provider
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        return services;
    }
}
