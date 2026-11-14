using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Identity;
using Aonik.Platform.Services.Integrations;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Application.Tests.Platform.Integrations;

internal sealed class ConnectorScenario : IDisposable
{
    private readonly ServiceProvider _provider;
    public Guid TenantId { get; } = Guid.NewGuid();
    public Guid ReviewerId { get; } = Guid.NewGuid();
    public Guid OutsiderId { get; } = Guid.NewGuid();
    public Guid ReviewerRoleId { get; } = Guid.NewGuid();
    public Actor Current { get; } = new();
    public TestClock Clock { get; } = new();
    public HashSet<(Guid, string)> Denied { get; } = [];
    public HashSet<(Guid, string)> Allowed { get; } = [];
    public Mock<IConnectorOAuthProvider> OAuth { get; } = new();
    public Mock<IConnectorActionProvider> Actions { get; } = new();
    public Mock<IAuditLogWriter> Audit { get; } = new();
    public Mock<ITaskService> Tasks { get; } = new();
    public ConnectorOAuthTokens Tokens { get; set; }
    public ConnectorAccountIdentity Account { get; set; } = new("google-account-1", "mailbox@example.test");
    public bool Provisioned { get; set; } = true;

    public ConnectorScenario(DbContextOptions<PlatformDbContext>? sqlOptions = null, Action<IServiceCollection>? configure = null)
    {
        Tokens = new("synthetic-access", "synthetic-refresh", Clock.UtcNow.AddHours(1), [GmailActionKeys.ReadScope, "openid", "email"]);
        OAuth.SetupGet(provider => provider.ConnectorKey).Returns("gmail");
        OAuth.SetupGet(provider => provider.IsConfigured).Returns(() => Provisioned);
        OAuth.Setup(provider => provider.CreateAuthorization(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>()))
            .Returns((IReadOnlyList<string> _, string state) => new("https://accounts.google.com/o/oauth2/auth?state=" + Uri.EscapeDataString(state), "synthetic-verifier"));
        OAuth.Setup(provider => provider.ExchangeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Tokens);
        OAuth.Setup(provider => provider.RefreshAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Tokens);
        OAuth.Setup(provider => provider.GetIdentityAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Account);
        Actions.SetupGet(provider => provider.ConnectorKey).Returns("gmail");
        Actions.Setup(provider => provider.InvokeAsync(It.IsAny<ConnectorProviderCall>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectorJson.Element(new GmailSearchResult([], null)));
        Audit.Setup(writer => writer.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        Tasks.Setup(service => service.ScheduleAsync(It.IsAny<ScheduleTaskRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync((TaskResponse)null!);
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(service => service.HasPermissionAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, string permission, CancellationToken _) => !Denied.Contains((userId, permission))
                && (Allowed.Contains((userId, permission)) || permission == "Integration.Read" || (userId == Current.OwnerId && permission != "Integration.Manage")
                    || (userId == ReviewerId && permission == "Integration.Manage")));
        var services = new ServiceCollection();
        services.AddSingleton<ITenantProvider>(new TestTenantProvider(TenantId));
        services.AddSingleton<ICurrentUserProvider>(Current);
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(permissions.Object); services.AddSingleton(OAuth.Object); services.AddSingleton(Actions.Object);
        services.AddSingleton(Audit.Object); services.AddSingleton(Tasks.Object);
        services.AddSingleton<IConnectorContentStore>(Mock.Of<IConnectorContentStore>());
        string name = $"ConnectorScenario_{Guid.NewGuid()}";
        services.AddDbContext<PlatformDbContext>(options => { if (sqlOptions is null) options.UseInMemoryDatabase(name); }, optionsLifetime: ServiceLifetime.Scoped);
        if (sqlOptions is not null) services.AddScoped(_ => sqlOptions);
        services.AddScoped<IUserDirectoryReader, UserDirectoryReader>();
        services.AddScoped<ITenantRoleReader, TenantRoleReader>();
        services.AddSingleton<IConnectorDescriptor, GmailConnectorDescriptor>();
        services.AddScoped<ConnectorCatalog>(); services.AddScoped<ConnectorPolicyService>(); services.AddScoped<ConnectorAuthorization>();
        services.AddScoped<ConnectorCredentialProtector>(); services.AddScoped<ConnectorCredentialService>(); services.AddScoped<ConnectorGrantService>();
        services.AddScoped<ConnectorConnectionRequestService>();
        services.AddScoped<IConnectorConnectionRequests>(provider => provider.GetRequiredService<ConnectorConnectionRequestService>());
        services.AddScoped<ConnectorConsentService>(); services.AddScoped<ConnectorService>();
        services.AddScoped<ConnectorRequestWakeHandler>(); services.AddScoped<ConnectorRequestExpiryHandler>();
        services.AddSingleton<ConnectorRequestRateLimiter>();
        services.AddScoped<ConnectorConnectionManagementService>();
        configure?.Invoke(services);
        _provider = services.BuildServiceProvider();
    }
    public IServiceScope Scope() => _provider.CreateScope();
    public async Task InitializeAsync(bool enabled = true, bool selfService = true)
    {
        using IServiceScope scope = Scope();
        PlatformDbContext db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.Users.AddRange(new User { Id = Current.OwnerId, TenantId = TenantId, Status = "Active", ExternalSubject = Current.OwnerId.ToString() },
            new User { Id = ReviewerId, TenantId = TenantId, Status = "Active", ExternalSubject = ReviewerId.ToString() },
            new User { Id = OutsiderId, TenantId = TenantId, Status = "Active", ExternalSubject = OutsiderId.ToString() });
        db.Roles.Add(new Role { Id = ReviewerRoleId, TenantId = TenantId, Name = "Integration reviewers" });
        db.UserRoles.Add(new() { UserId = ReviewerId, RoleId = ReviewerRoleId });
        if (enabled) db.Settings.Add(new Setting { Key = IntegrationSettingNames.GmailEnabled, Value = "true", Scope = SettingScope.Tenant, TenantId = TenantId });
        db.Settings.AddRange(new Setting { Key = IntegrationSettingNames.GmailAllowSelfService, Value = selfService ? "true" : "false", Scope = SettingScope.Tenant, TenantId = TenantId },
            new Setting { Key = IntegrationSettingNames.RequestReviewerRoleId, Value = ReviewerRoleId.ToString(), Scope = SettingScope.Tenant, TenantId = TenantId });
        await db.SaveChangesAsync();
    }
    public CreateConnectionRequest Request(string key = "gmail-read-1") => new("gmail", "User", [new(GmailActionKeys.Search, 1)], "Review invoice correspondence", key);
    public static string State(ConnectorConsentChallenge challenge) => Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(challenge.AuthorizationUrl).Query)["state"].ToString();
    public void Dispose() => _provider.Dispose();
    internal sealed class Actor : ICurrentUserProvider
    {
        public Guid OwnerId { get; } = Guid.NewGuid();
        private Guid? _id;
        public Guid Id { get => _id ?? OwnerId; set => _id = value; }
        public Guid? GetCurrentUserId() => Id;
        public bool TryGetCurrentUserId(out Guid id) { id = Id; return true; }
    }
    internal sealed class TestClock : IClock { public DateTime UtcNow { get; set; } = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc); }
}
