using Aonik.Infrastructure.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

using FluentAssertions;

using Microsoft.Extensions.Configuration;

using Moq;

namespace Aonik.Infrastructure.Tests.Settings;

/// <summary>
/// The tenant is the product, and its operator configures the AI provider and key it runs on. So
/// every AI setting resolves tenant first, then the platform's value (global, then the configuration
/// seed the Settings module reads), then the legacy appsettings fallback, then the default — and a
/// call with no tenant never asks for a tenant value.
/// </summary>
public class AiProviderSettingsTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public void TheTenantsValues_Should_WinOverThePlatforms()
    {
        var settings = Build(
            tenant: new() { [AiSettingNames.Provider] = "OpenAI", [AiSettingNames.OpenAiApiKey] = "sk-tenant", [AiSettingNames.OpenAiModel] = "gpt-5" },
            platform: new() { [AiSettingNames.Provider] = "Stub", [AiSettingNames.OpenAiApiKey] = "sk-platform", [AiSettingNames.OpenAiModel] = "gpt-5-mini", [AiSettingNames.OpenAiImageModel] = "gpt-image-1" },
            tenantId: Tenant);

        settings.Provider.Should().Be("OpenAI");
        settings.OpenAiApiKey.Should().Be("sk-tenant");
        settings.OpenAiModel.Should().Be("gpt-5");
        // The tenant set no image model: the platform's.
        settings.OpenAiImageModel.Should().Be("gpt-image-1");
    }

    [Fact]
    public void ATenantThatSetNothing_Should_RunOnThePlatforms_ThenTheLegacyConfiguration_ThenTheDefaults()
    {
        var platform = Build(tenant: new(), platform: new() { [AiSettingNames.OpenAiApiKey] = "sk-platform" }, tenantId: Tenant);
        platform.OpenAiApiKey.Should().Be("sk-platform");
        platform.Provider.Should().Be("Stub");
        platform.OpenAiModel.Should().Be("gpt-5-mini");
        platform.OpenAiImageModel.Should().Be("dall-e-3");

        var legacy = Build(tenant: new(), platform: new(), tenantId: Tenant, configuration: new() { ["AI:Provider"] = "OpenAI", ["AI:OpenAI:ApiKey"] = "sk-appsettings" });
        legacy.Provider.Should().Be("OpenAI");
        legacy.OpenAiApiKey.Should().Be("sk-appsettings");
    }

    [Fact]
    public void ACallWithNoTenant_Should_NeverAskForATenantValue()
    {
        var tenantSettings = new Mock<ITenantSettingStore>(MockBehavior.Strict);
        var settings = Build(tenantSettings, platform: new() { [AiSettingNames.OpenAiApiKey] = "sk-platform" }, tenantId: null);

        settings.OpenAiApiKey.Should().Be("sk-platform");
        tenantSettings.VerifyNoOtherCalls();
    }

    private static AiProviderSettings Build(Dictionary<string, string?> tenant, Dictionary<string, string?> platform, Guid? tenantId, Dictionary<string, string?>? configuration = null)
    {
        var tenantSettings = new Mock<ITenantSettingStore>();
        tenantSettings
            .Setup(s => s.GetTenantValueAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, Guid _, CancellationToken _) => tenant.GetValueOrDefault(key));
        return Build(tenantSettings, platform, tenantId, configuration);
    }

    private static AiProviderSettings Build(Mock<ITenantSettingStore> tenantSettings, Dictionary<string, string?> platform, Guid? tenantId, Dictionary<string, string?>? configuration = null)
    {
        var provider = new Mock<ISettingProvider>();
        provider
            .Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => platform.GetValueOrDefault(key));
        var tenants = new Mock<ITenantProvider>();
        var id = tenantId ?? Guid.Empty;
        tenants.Setup(t => t.TryGetCurrentTenantId(out id)).Returns(tenantId.HasValue);
        var config = new ConfigurationBuilder().AddInMemoryCollection(configuration ?? new Dictionary<string, string?>()).Build();

        return new AiProviderSettings(new TenantFirstSettingReader(tenantSettings.Object, provider.Object, tenants.Object), config);
    }
}
