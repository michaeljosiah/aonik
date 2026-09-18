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
using Aonik.SharedKernel.Persistence;
using Aonik.Finance.Entities.Orders;
using Aonik.Platform.Contracts.Services.Messaging;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Infrastructure.Persistence;


namespace Aonik.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Use a consistent database name per factory instance
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    /// <summary>
    /// When set, every DbContext runs against this SQL Server database instead of the InMemory provider.
    /// For endpoints whose guarantees the InMemory provider cannot express — <c>ExecuteUpdateAsync</c>,
    /// RowVersion concurrency, unique indexes — per CLAUDE.md's LocalDB-lane rule. The database must
    /// already exist with its schema (see <c>SqlLocalDbFixture</c>); startup applies no migrations in Testing.
    /// </summary>
    private readonly string? _sqlServerConnectionString;

    public CustomWebApplicationFactory()
    {
    }

    /// <summary>xUnit allows a class fixture one public constructor; the SQL variant is the subclass below.</summary>
    protected CustomWebApplicationFactory(string sqlServerConnectionString)
    {
        _sqlServerConnectionString = sqlServerConnectionString;
    }

    public bool UsesSqlServer => _sqlServerConnectionString is not null;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = UsesSqlServer ? "false" : "true",
                ["InMemoryDatabaseName"] = _databaseName,
                ["ConnectionStrings:DefaultConnection"] = _sqlServerConnectionString,
                ["Auth:TenantRouting"] = "Claim",
                ["Bootstrap:Enabled"] = "true",
                ["Bootstrap:SetupSecret"] = "test-install-code",
                ["Bootstrap:TenantName"] = "Bootstrap Test Tenant",
                ["PlatformAdmin:AdminEmails:0"] = "bootstrap-admin@example.com",
                ["Operations:Alerts:AzureMonitor:SharedSecret"] = "test-alert-secret",
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
            services.RemoveAll<IPushNotificationSender>();
            services.AddSingleton<IEmailSender, TestEmailSender>();
            services.AddSingleton<ISmsSender, TestSmsSender>();
            services.AddSingleton<TestPushNotificationSender>();
            services.AddSingleton<IPushNotificationSender>(sp => sp.GetRequiredService<TestPushNotificationSender>());

            if (!UsesSqlServer)
            {
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
            }
            else
            {
                // Infrastructure registers no canonical context in the Testing environment; the module
                // contexts already read the connection string, so the canonical one follows them here.
                services.AddDbContext<AonikDbContext>(options =>
                {
                    options.UseSqlServer(_sqlServerConnectionString!, sql => sql.EnableRetryOnFailure());
                });

                services.AddScoped<IAonikDbContext>(sp => sp.GetRequiredService<AonikDbContext>());
            }

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
        public bool IsConfigured => true;
        public string ProviderName => "Test";
        public string? UnconfiguredReason => null;
    }

    private sealed class TestSmsSender : ISmsSender
    {
        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool IsConfigured => true;
        public string ProviderName => "Test";
        public string? UnconfiguredReason => null;
    }

    public sealed class TestPushNotificationSender : IPushNotificationSender
    {
        private readonly object _sync = new();
        private readonly List<PushNotificationDispatchRequest> _requests = new();
        private HashSet<Guid> _invalidDeviceIds = new();

        public IReadOnlyList<PushNotificationDispatchRequest> Requests
        {
            get
            {
                lock (_sync)
                {
                    return _requests.ToList();
                }
            }
        }

        public void Reset()
        {
            lock (_sync)
            {
                _requests.Clear();
                _invalidDeviceIds.Clear();
            }
        }

        public void SetInvalidDeviceIds(params Guid[] invalidDeviceIds)
        {
            lock (_sync)
            {
                _invalidDeviceIds = invalidDeviceIds.ToHashSet();
            }
        }

        public Task<PushNotificationDispatchResult> SendAsync(
            PushNotificationDispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _requests.Add(request);

                var invalidIds = request.Targets
                    .Where(x => _invalidDeviceIds.Contains(x.NotificationDeviceId))
                    .Select(x => x.NotificationDeviceId)
                    .ToList();

                return Task.FromResult<PushNotificationDispatchResult>(new PushNotificationDispatchResult(invalidIds));
            }
        }
    }

    public TestPushNotificationSender GetPushNotificationSender()
    {
        return Services.GetRequiredService<TestPushNotificationSender>();
    }

    /// <summary>
    /// Seeds a bill-payment Order so payment-intent flows have a real, tenant-scoped order
    /// to resolve the payer from. Returns the new order id.
    /// </summary>
    public async Task<Guid> SeedOrderAsync(
        Guid tenantId,
        Guid? payerPartyId = null,
        string status = "Draft",
        decimal amountIn = 100m,
        string currencyIn = "USD")
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AonikDbContext>();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderType = "BillPayment",
            PayerPartyId = payerPartyId,
            AmountIn = amountIn,
            CurrencyIn = currencyIn,
            Status = status,
            FeesJson = "[]",
            ProvenanceJson = "{}"
        };

        dbContext.Set<Order>().Add(order);
        await dbContext.SaveChangesAsync();
        return order.Id;
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
                // Unique per tenant: AnkTenants has a unique index on Name that the SQL lane enforces.
                Name = $"Test Tenant {tenantId:N}",
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
                // Unique per user: AnkUsers has a unique index on (TenantId, ExternalIssuer, ExternalSubject).
                ExternalSubject = options.UserId.ToString(),
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
            .AcrossTenants()
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

/// <summary>The API over a SQL Server database that already carries the schema, for the LocalDB lane.</summary>
public sealed class SqlServerWebApplicationFactory : CustomWebApplicationFactory
{
    public SqlServerWebApplicationFactory(string sqlServerConnectionString) : base(sqlServerConnectionString)
    {
    }
}
