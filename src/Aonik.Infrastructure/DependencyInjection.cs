using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Aonik.Application.Abstractions.Ai;
using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Messaging;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Abstractions.ReferenceData;
using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Options;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Application.Services.Identity.Provisioning;
using Aonik.Application.Services.Registration;
using Aonik.Application.Services.Settings;
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
using Aonik.Infrastructure.Observability;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Storage;
using Aonik.Infrastructure.BackgroundJobs;
using Aonik.Infrastructure.Time;
using Aonik.SharedKernel.Abstractions;


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
        services.Configure<BootstrapOptions>(configuration.GetSection("Bootstrap"));
        services.Configure<PlatformAdminOptions>(configuration.GetSection("PlatformAdmin"));
        services.Configure<CommunicationOptions>(configuration.GetSection("Communication"));
        services.Configure<OnboardingPolicyOptions>(configuration.GetSection("OnboardingPolicy"));
        services.Configure<VerificationOptions>(configuration.GetSection("Verification"));
        services.Configure<CustomerProfileStorageOptions>(configuration.GetSection("ProfileStorage"));
        services.AddMemoryCache();
        services.AddDataProtection();

        services.AddSingleton<IBlobStorage>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CustomerProfileStorageOptions>>().Value;
            return CustomerProfileBlobStorageFactory.Create(options.LocalStoragePath);
        });
        services.AddHostedService<ProfilePhotoStorageInitializer>();

        // Multitenancy

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantProvider, HttpContextTenantProvider>();

        // Database - environment-aware selection
        var useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase");
        var inMemoryName = configuration["InMemoryDatabaseName"] ?? "AonikTestDb";

        if (environment.IsEnvironment("Testing"))
        {
            services.AddDbContext<AonikDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(inMemoryName);
            });
        }
        else if (environment.IsDevelopment() && useInMemory)
        {
            services.AddDbContext<AonikDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(inMemoryName);
            });
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
                    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for SQL Server in this environment.");
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
        services.AddScoped<IBootstrapService, BootstrapService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<ISettingValueProtector, SettingValueProtector>();
        services.AddScoped<ISettingProvider, SettingService>();
        services.AddScoped<ISettingManager, SettingService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IAuthProviderSettingsService, AuthProviderSettingsService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IIdentityService, IdentityService>();
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
            // Platform admin policy (application-level role)
            options.AddPolicy("PlatformAdmin", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["PlatformAdmin"],
                    Array.Empty<string>())));


            options.AddPolicy("TenantAdmin", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["TenantAdmin"],
                    ["Users.Manage"])));

            options.AddPolicy("CanOperate", policy =>
                policy.Requirements.Add(new RoleOrPermissionRequirement(
                    ["Operations"],
                    ["Payment.Create"])));
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
