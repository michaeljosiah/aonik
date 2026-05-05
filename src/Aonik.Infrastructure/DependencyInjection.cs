using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Retry;
using Aonik.Application.Abstractions;
using Aonik.Platform.Contracts.Services.Authentication;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Contracts.Services.Operations;
using Aonik.SharedKernel.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Platform.Contracts.Services.ReferenceData;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.Application.Abstractions.Storage;
using Aonik.Application.Options;
using Aonik.Platform.Contracts.Services.Observability;
using Aonik.Platform.Contracts.Services.Storage;
using Aonik.Platform.Services.Onboarding;

using Aonik.Platform.Services.Identity;
using Aonik.Platform.Services.Registration;
using Aonik.Platform.Contracts.Services.Identity;

using Aonik.Platform.Services.Notifications;
using Aonik.Platform.Contracts.Services.Registration;
using Aonik.Platform.Contracts.Services.Onboarding;
using Aonik.SharedKernel.Abstractions;

using Aonik.Platform.Contracts.Models.Configuration;
using Aonik.Ai.Contracts.Services;
using Aonik.Infrastructure.Authentication;
using Aonik.Infrastructure.Ai.ModelCatalog;
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
using Aonik.Infrastructure.Operations;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Storage;
using Aonik.Infrastructure.BackgroundJobs;
using Aonik.Infrastructure.Caching;
using Aonik.Infrastructure.Time;
using Aonik.Infrastructure.Features;
using Aonik.SharedKernel.Caching;
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
        services.Configure<PlatformAdminOptions>(configuration.GetSection("PlatformAdmin"));
        services.Configure<CommunicationOptions>(configuration.GetSection("Communication"));
        services.Configure<FcmOptions>(configuration.GetSection("Notifications:Fcm"));
        services.Configure<ContainerAppsRuntimeOptions>(configuration.GetSection("Runtime:AzureContainerApps"));
        services.Configure<BlobStorageOptions>(configuration.GetSection("BlobStorage"));
        services.AddMemoryCache();
        services.AddFusionCache();

        services.AddSingleton<CachePolicyProvider>();
        services.AddSingleton<CacheSetRegistry>();
        services.AddSingleton<ICacheInvalidationPublisher, CacheInvalidationPublisher>();
        services.AddSingleton<FusionCacheInvalidationHandler>();
        services.AddHostedService<CacheInvalidationSubscriptionService>();
        services.AddScoped<ICacheStore, FusionCacheStore>();
        services.AddScoped<ICacheManagementService, CacheManagementService>();

        services.AddFeatureManagement()
            .AddFeatureFilter<TenantFeatureFilter>();

        services.AddScoped<IFeatureManager, DatabaseFeatureManager>();

        // Blob Storage factory (shared provider, content-type aware)
        services.AddSingleton<IBlobStorageFactory, BlobStorageFactoryService>();

        // Image Processing Service
        services.AddScoped<IImageProcessingService, ImageProcessingService>();

        // Profile Photo Store abstraction
        services.AddScoped<IProfilePhotoStore, ProfilePhotoStore>();
        
        services.AddScoped<IDocumentFileStore, DocumentFileStore>();

        // Generic file store for attachments (transaction receipts, etc.)
        services.AddScoped<Aonik.SharedKernel.Abstractions.Storage.IFileStore>(sp =>
        {
            var blobStorageFactory = sp.GetRequiredService<Aonik.Application.Abstractions.Storage.IBlobStorageFactory>();
            var storageOptions = sp.GetRequiredService<IOptions<BlobStorageOptions>>();
            return new FileStore(blobStorageFactory, storageOptions, storageOptions.Value.Attachments);
        });

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
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration.GetConnectionString("AonikDb");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                if (environment.IsDevelopment())
                {
                    connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";
                }
                else
                {
                    throw new InvalidOperationException("ConnectionStrings:DefaultConnection or ConnectionStrings:AonikDb is required for SQL Server.");
                }
            }

            services.AddDbContext<AonikDbContext>((sp, options) =>
            {
                options.UseSqlServer(connectionString, sqlServerOptions =>
                    sqlServerOptions.EnableRetryOnFailure());
            });
        }

        services.AddDataProtection()
            .SetApplicationName("Aonik")
            .PersistKeysToDbContext<AonikDbContext>();

        services.AddScoped<IAonikDbContext>(sp => sp.GetRequiredService<AonikDbContext>());

        // Infrastructure Services (implementations that wrap external systems)
        services.AddScoped<ISettingValueProtector, SettingValueProtector>();
        services.AddScoped<ISettingProvider, SettingService>();
        services.AddScoped<ISettingManager, SettingService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddHttpClient<IAiModelCatalogSource, ModelsDevAiModelCatalogSource>((_, client) =>
        {
            client.BaseAddress = new Uri(configuration["AI:ModelCatalog:BaseAddress"] ?? "https://models.dev", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(configuration.GetValue<int?>("AI:ModelCatalog:TimeoutSeconds") ?? 30);
        });
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
        services.AddSingleton<INotificationTemplateRenderer, FluidNotificationTemplateRenderer>();
        services.AddHttpClient<IPushNotificationSender, FirebasePushNotificationSender>();



        // Application Insights observability queries
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is the supported way to replace the inherited default handler for this named client.
        services.AddHttpClient("AppInsights", client =>
        {
            client.BaseAddress = new Uri("https://api.applicationinsights.io/v1/apps/");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .RemoveAllResilienceHandlers()
        .AddStandardResilienceHandler(options =>
        {
            // App Insights KQL queries can legitimately take longer than the
            // 10s default per-attempt timeout used by the shared standard
            // resilience pipeline. Keep retries/circuit breaking, but widen
            // the overall budget to match the client timeout configured above
            // without violating the standard circuit-breaker validation rules.
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
        });
#pragma warning restore EXTEXP0001
        services.AddHttpClient("AzureResourceManager", client =>
        {
            client.BaseAddress = new Uri(configuration["Runtime:AzureContainerApps:ManagementBaseUrl"] ?? "https://management.azure.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IObservabilityService, Observability.AppInsightsQueryService>();
        services.AddScoped<IRuntimeOperationsService, ContainerAppsRuntimeService>();

        // Background jobs core services. Quartz runtime registration is owned by execution hosts.
        services.AddAonikBackgroundJobCoreServices();

        // Vector Store (Qdrant) for RAG capabilities
        services.Configure<Aonik.Infrastructure.VectorStore.Qdrant.QdrantConfiguration>(
            configuration.GetSection("Qdrant"));

        // HTTP client for Qdrant.
        //
        // ServiceDefaults.ConfigureHttpClientDefaults attaches the
        // Microsoft.Extensions.Http.Resilience StandardResilienceHandler
        // to every HttpClient by default — for Qdrant that means each
        // failed call retries up to 3 times with a 10 s AttemptTimeout
        // each. When dev Qdrant is slow / restarting, a single
        // /readyz call becomes ~30 s of blocking work and emits ~3
        // SocketException + 3 TaskCanceledException entries at Error
        // severity (the bulk of the platform's "incidents" panel —
        // 75+ events per restart). Override the standard handler with
        // a tighter, Qdrant-specific config so a slow Qdrant is loud
        // exactly once, not three times, and recovers in seconds
        // rather than tens of seconds.
        services.AddHttpClient<Aonik.Infrastructure.VectorStore.Qdrant.QdrantHttpClient>((sp, client) =>
        {
            var config = sp.GetRequiredService<IOptions<Aonik.Infrastructure.VectorStore.Qdrant.QdrantConfiguration>>().Value;
            client.BaseAddress = new Uri(config.Endpoint);
            client.DefaultRequestHeaders.Add("api-key", config.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(config.Timeout);
        })
        .AddStandardResilienceHandler(options =>
        {
            // 3 s is generous for a healthy Qdrant /readyz (typically
            // <100 ms) but still allows a real query to complete. The
            // overall HttpClient.Timeout (config-driven, default 30 s)
            // remains the absolute upper bound for the WHOLE request.
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
            // QdrantCollectionInitializer already runs its own
            // 3-attempt loop with a 2 s delay between attempts; one
            // additional retry inside Polly is enough belt-and-braces
            // for transient packet loss without compounding the noise.
            options.Retry.MaxRetryAttempts = 1;
        });

        // Vector store and embedding services
        services.AddScoped<Aonik.Infrastructure.VectorStore.Contracts.IVectorStore,
            Aonik.Infrastructure.VectorStore.Qdrant.QdrantVectorStore>();
        services.AddScoped<Aonik.Infrastructure.VectorStore.Contracts.IEmbeddingService,
            Aonik.Infrastructure.VectorStore.Providers.OpenAiEmbeddingService>();

        // Collection initializer
        services.AddHostedService<Aonik.Infrastructure.VectorStore.Qdrant.QdrantCollectionInitializer>();

        // OpenTelemetry metrics for vector store
        services.AddSingleton<Aonik.Infrastructure.VectorStore.QdrantMetrics>();

        // User memory backend selection. The Ai.UserMemory.Backend setting
        // is global (not tenant- or user-scoped) and operationally only
        // changes via a process restart, so we resolve it ONCE at startup
        // (see InitializeUserMemoryBackendAsync) and cache the choice on a
        // singleton. The per-request factory below is then sync-only and
        // does not block a thread-pool thread on a DB lookup, which is
        // what the previous .GetAwaiter().GetResult() did per scoped
        // resolution.
        services.AddSingleton<Aonik.Infrastructure.VectorStore.UserMemoryBackendSelection>();
        services.AddScoped<Aonik.Infrastructure.VectorStore.QdrantUserMemoryService>();
        services.AddScoped<Aonik.Ai.Contracts.Services.IUserMemoryService>(sp =>
        {
            var selection = sp.GetRequiredService<Aonik.Infrastructure.VectorStore.UserMemoryBackendSelection>();
            return selection.IsQdrant
                ? sp.GetRequiredService<Aonik.Infrastructure.VectorStore.QdrantUserMemoryService>()
                : sp.GetRequiredService<Aonik.Ai.Services.UserMemoryService>();
        });

        // AI provider settings — resolves from Settings module with IConfiguration fallback.
        // Registered here (not in AiModule) because the implementation needs ISettingProvider
        // from Aonik.Platform, which Aonik.Ai does not reference.
        services.AddScoped<Aonik.SharedKernel.Abstractions.Ai.IAiProviderSettings,
            Aonik.Infrastructure.Settings.AiProviderSettings>();

        // Domain readiness probes for the two external systems on the
        // request path. Both are tagged "ready" so they show up at
        // /health (the readiness probe) but not /alive — a SQL or
        // Qdrant outage takes the pod out of rotation, it does not
        // recycle the running process. ServiceDefaults' base "self"
        // check stays the only "live" entry.
        services.AddHealthChecks()
            .AddCheck<Aonik.Infrastructure.Health.SqlServerHealthCheck>(
                name: "sql-server",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["ready", "db", "sql"],
                timeout: TimeSpan.FromSeconds(5))
            .AddCheck<Aonik.Infrastructure.Health.QdrantHealthCheck>(
                name: "qdrant",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
                tags: ["ready", "vector-store", "qdrant"],
                timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    public static IServiceCollection AddAonikAuthenticationAndAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register authentication services
        services.AddScoped<ITenantResolver, TenantResolver>();

        // Add authentication
        services.AddAonikAuthentication(configuration);

        // Add authorization
        services.AddAuthorization(options =>
        {
            // Role-based policies (API boundary)
            options.AddPolicy("PlatformAdmin", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new PlatformAdminRequirement());
            });

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
