using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.Infrastructure.Persistence;


namespace Aonik.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Use a consistent database name per factory instance
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["InMemoryDatabaseName"] = _databaseName,
                ["Auth:TenantRouting"] = "Claim",
                ["Bootstrap:Enabled"] = "true",
                ["Bootstrap:SetupSecret"] = "test-install-code",
                ["Bootstrap:TenantName"] = "Bootstrap Test Tenant",
                ["PlatformAdmin:AdminEmails:0"] = "bootstrap-admin@example.com",
                ["BlobStorage:Provider"] = "Local",
                ["BlobStorage:LocalBasePath"] = $"App_Data/Test_{_databaseName}",
                ["BlobStorage:ProfilePhotos:Path"] = $"profiles_{_databaseName}",
                ["BlobStorage:ProfilePhotos:ContainerName"] = $"profiles_{_databaseName}",
                ["BlobStorage:ProfilePhotos:PublicBaseUrl"] = ""
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove ProfilePhotoStorageInitializer to avoid file locking issues in parallel tests
            services.RemoveAll<IHostedService>();
            
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<ISmsSender>();
            services.AddSingleton<IEmailSender, TestEmailSender>();
            services.AddSingleton<ISmsSender, TestSmsSender>();

            // Remove existing DbContext registration and replace with InMemory
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AonikDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Also remove IAonikDbContext if it was registered
            var interfaceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAonikDbContext));
            if (interfaceDescriptor != null)
            {
                services.Remove(interfaceDescriptor);
            }

            // Add InMemory DbContext for tests with CONSISTENT database name
            services.AddDbContext<AonikDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Register IAonikDbContext
            services.AddScoped<IAonikDbContext>(sp => sp.GetRequiredService<AonikDbContext>());

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName,
                    _ => { });

            services.PostConfigure<AuthorizationOptions>(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
            db.Database.EnsureCreated();

        });
    }

    private sealed class TestEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestSmsSender : ISmsSender
    {
        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant == null)
        {
            tenant = new Tenant
            {
                Id = tenantId,
                Name = "Test Tenant",
                Environment = Environments.Development,
                DefaultCurrency = "USD",
                SupportedCountriesJson = "[]",
                Status = TenantStatus.Active
            };

            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync();
        }

        // Seed shared notification templates (needed by VerificationService for OTP rendering)
        await SeedNotificationTemplatesAsync(scope.ServiceProvider);

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
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = roleName
        };


        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        foreach (var permission in permissions)
        {
            dbContext.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id,
                Role = role,
                Permission = permission
            });

        }

        dbContext.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            Role = role,
            User = user
        });


        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedNotificationTemplatesAsync(IServiceProvider sp)
    {
        var platformDb = sp.GetRequiredService<PlatformDbContext>();

        var alreadySeeded = await platformDb.NotificationTemplates
            .IgnoreQueryFilters()
            .AnyAsync(t => t.TenantId == null && t.IsShared);

        if (alreadySeeded) return;

        platformDb.NotificationTemplates.AddRange(
            new NotificationTemplate
            {
                Name = NotificationTemplateNames.SmsOtp,
                Channel = "SMS",
                IsShared = true,
                IsActive = true,
                Description = "Test SMS OTP template",
                SubjectTemplate = "",
                BodyTemplate = "Your code is {{ otp_code }}."
            },
            new NotificationTemplate
            {
                Name = NotificationTemplateNames.EmailOtp,
                Channel = "Email",
                IsShared = true,
                IsActive = true,
                Description = "Test Email OTP template",
                SubjectTemplate = "Code: {{ otp_code }}",
                BodyTemplate = "Your code is {{ otp_code }}."
            },
            new NotificationTemplate
            {
                Name = NotificationTemplateNames.EmailConfirmation,
                Channel = "Email",
                IsShared = true,
                IsActive = true,
                Description = "Test Email confirmation template",
                SubjectTemplate = "Confirm your email",
                BodyTemplate = "<a href=\"{{ confirmation_url }}\">Confirm</a>"
            },
            new NotificationTemplate
            {
                Name = NotificationTemplateNames.WelcomeEmail,
                Channel = "Email",
                IsShared = true,
                IsActive = true,
                Description = "Test welcome email template",
                SubjectTemplate = "Welcome!",
                BodyTemplate = "Welcome, {{ first_name }}!"
            });

        await platformDb.SaveChangesAsync();
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
                Id = Guid.NewGuid(),
                Key = key,
                Description = $"Test permission for {key}"
            });

        }

        foreach (var permission in existing.Where(permission => permission.Id == Guid.Empty))
        {
            permission.Id = Guid.NewGuid();
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync();
        }

        var result = await dbContext.Permissions
            .Where(p => keys.Contains(p.Key))
            .GroupBy(p => p.Key)
            .Select(g => g.OrderByDescending(p => p.CreatedAt).First())
            .ToListAsync();

        result.Should().HaveCount(keys.Count);

        return result;

    }

    private static string SerializeClaims(IEnumerable<Claim> claims)
    {
        return string.Join(";", claims.Select(claim => $"{claim.Type}={claim.Value}"));
    }
}
