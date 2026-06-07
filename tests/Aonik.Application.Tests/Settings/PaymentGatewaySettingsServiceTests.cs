using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using FluentAssertions;

namespace Aonik.Application.Tests.Settings;

public class PaymentGatewaySettingsServiceTests
{
    [Fact]
    public async Task GetAsync_Should_ReturnHasSecretFlags_WithoutSecretValues()
    {
        var store = new InMemorySettings(new Dictionary<string, string?>
        {
            [PartnerGatewaySettingNames.FlutterwaveEnabled] = "true",
            [PartnerGatewaySettingNames.FlutterwaveClientId] = "client-id",
            [PartnerGatewaySettingNames.FlutterwaveClientSecret] = "super-secret",
            [PartnerGatewaySettingNames.FlutterwaveEncryptionKey] = "enc-key",
            [PartnerGatewaySettingNames.FlutterwaveSigningSecret] = "wh-secret"
        });
        var service = CreateService(store);

        var result = await service.GetAsync();

        var flutterwave = result.Providers.Should().ContainSingle().Subject;
        flutterwave.Enabled.Should().BeTrue();
        flutterwave.ClientId.Should().Be("client-id");
        flutterwave.HasClientSecret.Should().BeTrue();
        flutterwave.HasEncryptionKey.Should().BeTrue();
        flutterwave.HasSigningSecret.Should().BeTrue();
        flutterwave.SecretSource.Should().Be("Database");
        flutterwave.ToString().Should().NotContain("super-secret");
    }

    [Fact]
    public async Task UpdateAsync_Should_KeepExistingSecrets_When_SecretFieldsBlank()
    {
        var store = new InMemorySettings(new Dictionary<string, string?>
        {
            [PartnerGatewaySettingNames.FlutterwaveClientSecret] = "existing-secret",
            [PartnerGatewaySettingNames.FlutterwaveEncryptionKey] = "existing-encryption",
            [PartnerGatewaySettingNames.FlutterwaveSigningSecret] = "existing-signing"
        });
        var service = CreateService(store);

        await service.UpdateAsync(new PaymentGatewaySettingsUpdate(new[]
        {
            new PaymentGatewayProviderUpdate(
                "Flutterwave",
                true,
                "https://api.example.test",
                "https://idp.example.test/token",
                "client-id",
                "family_maintenance",
                ClientSecret: null,
                EncryptionKey: "",
                SigningSecret: "   ")
        }));

        store.Values[PartnerGatewaySettingNames.FlutterwaveClientSecret].Should().Be("existing-secret");
        store.Values[PartnerGatewaySettingNames.FlutterwaveEncryptionKey].Should().Be("existing-encryption");
        store.Values[PartnerGatewaySettingNames.FlutterwaveSigningSecret].Should().Be("existing-signing");
        store.Values[PartnerGatewaySettingNames.FlutterwaveBaseUrl].Should().Be("https://api.example.test");
    }

    private static PaymentGatewaySettingsService CreateService(InMemorySettings store)
        => new(
            store,
            store,
            new TestHttpClientFactory(),
            new NoOpAuditLogWriter(),
            new TestTenantProvider(Guid.NewGuid()),
            new TestCurrentUserProvider(Guid.NewGuid()));

    private sealed class InMemorySettings(IDictionary<string, string?> values) : ISettingProvider, Aonik.Platform.Contracts.Services.Settings.ISettingManager
    {
        public IDictionary<string, string?> Values { get; } = values;

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Values.TryGetValue(key, out var value) ? value : null);

        public async Task<string> GetRequiredAsync(string key, CancellationToken cancellationToken = default)
            => await GetAsync(key, cancellationToken) ?? throw new InvalidOperationException($"Setting '{key}' is required.");

        public Task<string?> GetForScopeAsync(
            string key,
            SettingScope scope,
            Guid? tenantId = null,
            Guid? userId = null,
            CancellationToken cancellationToken = default)
            => GetAsync(key, cancellationToken);

        public Task<SettingResolution> GetResolvedAsync(
            string key,
            Guid? tenantId = null,
            Guid? userId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new SettingResolution(
                key,
                Values.TryGetValue(key, out var value) ? value : null,
                Values.ContainsKey(key) ? "Global" : "None"));

        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
        {
            Values[key] = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            return Task.CompletedTask;
        }

        public Task SetAsync(
            string key,
            string? value,
            SettingScope scope,
            Guid? tenantId = null,
            Guid? userId = null,
            CancellationToken cancellationToken = default)
            => SetAsync(key, value, cancellationToken);

        public Task<bool> HasStoredValueAsync(
            string key,
            SettingScope scope,
            Guid? tenantId = null,
            Guid? userId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value));
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class NoOpAuditLogWriter : IAuditLogWriter
    {
        public Task LogAsync(
            string action,
            string resourceType,
            Guid resourceId,
            Guid tenantId,
            Guid? actorId,
            string? correlationId,
            string? detailsJson = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid id) { id = tenantId; return true; }
    }

    private sealed class TestCurrentUserProvider(Guid userId) : ICurrentUserProvider
    {
        public Guid? GetCurrentUserId() => userId;
        public bool TryGetCurrentUserId(out Guid id) { id = userId; return true; }
    }
}
