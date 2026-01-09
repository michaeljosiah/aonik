using Aonik.Application.Abstractions.Ai;
using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Services.Compliance;
using Aonik.Application.Services.Identity;
using Aonik.Application.Services.Identity.Provisioning;
using Aonik.Infrastructure.Ai.Prompting;
using Aonik.Infrastructure.Ai.Providers;
using Aonik.Infrastructure.Authentication;
using Aonik.Infrastructure.Authorization;
using Aonik.Infrastructure.Identity;
using Aonik.Infrastructure.Multitenancy;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Time;
using Aonik.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core abstractions
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<ICurrentUserProvider, HttpContextCurrentUserProvider>();

        // Multitenancy
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpContextTenantProvider>();

        // Database - support both SQL Server and InMemory for testing
        var useInMemory = configuration["UseInMemoryDatabase"];
        
        if (useInMemory == "true")
        {
            var dbName = configuration["InMemoryDatabaseName"] ?? "AonikTestDb";
            services.AddDbContext<AonikDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(dbName);
            });
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;";

            services.AddDbContext<AonikDbContext>((sp, options) =>
            {
                options.UseSqlServer(connectionString);
            });
        }

        services.AddScoped<IAonikDbContext>(sp => sp.GetRequiredService<AonikDbContext>());

        // Application Services
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ITenantProvisioner, TenantProvisioner>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();

        // AI
        services.AddSingleton<IPromptStore>(sp =>
        {
            var promptPath = configuration["AI:PromptTemplatesPath"];
            return new FileBasedPromptStore(promptPath);
        });

        services.AddScoped<IModelProvider, StubModelProvider>();

        return services;
    }
    
    public static IServiceCollection AddAonikAuthenticationAndAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register authentication services
        services.AddScoped<ITenantResolver, TenantResolver>();
        services.AddScoped<IUserIdentityService, UserIdentityService>();
        services.AddScoped<IPermissionService, PermissionService>();
        
        // Add authentication
        services.AddAonikAuthentication(configuration);
        
        // Add authorization
        services.AddAuthorization(options =>
        {
            // Platform admin policy
            options.AddPolicy("PlatformAdmin", policy =>
                policy.Requirements.Add(new PlatformAdminRequirement()));
        });
        
        // Register authorization handlers (SCOPED for permission handler)
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, PlatformAdminHandler>();
        
        // Register dynamic policy provider
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        
        return services;
    }
}
