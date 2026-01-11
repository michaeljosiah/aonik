using Aonik.Application.Abstractions.Ai;
using Aonik.Application.Abstractions.Authentication;
using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Observability;
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
using Aonik.Infrastructure.Observability;
using Aonik.Infrastructure.Persistence;
using Aonik.Infrastructure.Time;
using Aonik.SharedKernel.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        
        // Add authentication
        services.AddAonikAuthentication(configuration);
        
        // Add authorization
        services.AddAuthorization(options =>
        {
            // Platform admin policy
            options.AddPolicy("PlatformAdmin", policy =>
                policy.Requirements.Add(new PlatformAdminRequirement()));

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
