using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Domain.Identity.Entities;
using Aonik.Infrastructure.Persistence;

namespace Aonik.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Provide a unique database name per test run
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InMemoryDatabaseName"] = "TestDb_" + Guid.NewGuid().ToString(),
                ["Auth:TenantRouting"] = "Claim"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            db.Database.EnsureCreated();
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(TestAuthOptions options)
    {
        var client = CreateClient();
        var headers = client.DefaultRequestHeaders;

        headers.Add(TestAuthHandler.UserIdHeader, options.UserId.ToString());
        if (options.TenantId.HasValue)
        {
            headers.Add(TestAuthHandler.TenantIdHeader, options.TenantId.Value.ToString());
        }

        if (options.Roles.Count > 0)
        {
            headers.Add(TestAuthHandler.RolesHeader, string.Join(",", options.Roles));
        }

        if (options.Claims.Count > 0)
        {
            headers.Add(TestAuthHandler.ClaimsHeader, SerializeClaims(options.Claims));
        }

        await SeedIdentityAsync(options);

        return client;
    }

    private async Task SeedIdentityAsync(TestAuthOptions options)
    {
        if (!options.TenantId.HasValue)
        {
            return;
        }

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

        var tenantId = options.TenantId.Value;
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId);
        if (tenant == null)
        {
            tenant = new Tenant
            {
                TenantId = tenantId,
                Name = "Test Tenant",
                Environment = Environments.Development,
                DefaultCurrency = "USD",
                SupportedCountriesJson = "[]",
                Status = TenantStatus.Active
            };

            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync();
        }

        if (options.Permissions.Count == 0)
        {
            return;
        }

        var permissions = await EnsurePermissionsAsync(dbContext, options.Permissions);

        tenantContext.TenantId = tenantId;

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == options.UserId);
        if (user == null)
        {
            user = new User
            {
                Id = options.UserId,
                TenantId = tenantId,
                ExternalIssuer = "test",
                ExternalSubject = "test",
                Email = "test-user@example.com",
                Status = "Active"
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        var roleName = $"TestRole-{Guid.NewGuid()}";
        var role = new Role
        {
            RoleId = Guid.NewGuid(),
            TenantId = tenantId,
            Name = roleName
        };

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        foreach (var permission in permissions)
        {
            dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = role.RoleId,
                PermissionId = permission.PermissionId,
                Role = role,
                Permission = permission
            });
        }

        dbContext.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.RoleId,
            Role = role,
            User = user
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<List<Permission>> EnsurePermissionsAsync(
        AonikDbContext dbContext,
        IEnumerable<string> permissionKeys)
    {
        var keys = permissionKeys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existing = await dbContext.Permissions
            .Where(p => keys.Contains(p.Key))
            .ToListAsync();

        var existingKeys = existing.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingKeys = keys.Where(key => !existingKeys.Contains(key));

        foreach (var key in missingKeys)
        {
            dbContext.Permissions.Add(new Permission
            {
                PermissionId = Guid.NewGuid(),
                Key = key,
                Description = $"Test permission for {key}"
            });
        }

        foreach (var permission in existing.Where(permission => permission.PermissionId == Guid.Empty))
        {
            permission.PermissionId = Guid.NewGuid();
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync();
        }

        var result = await dbContext.Permissions
            .Where(p => keys.Contains(p.Key))
            .ToListAsync();

        result.Should().HaveCount(keys.Count);

        return result;
    }

    private static string SerializeClaims(IEnumerable<Claim> claims)
    {
        return string.Join(";", claims.Select(claim => $"{claim.Type}={claim.Value}"));
    }
}
