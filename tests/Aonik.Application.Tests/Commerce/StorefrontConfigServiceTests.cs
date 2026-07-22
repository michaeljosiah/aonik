using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Spec 070 §9 — the storefront-config document: defaults for an unconfigured tenant
/// (never 404), canonical tenant currency, write validation that leaves nothing half-updated.
/// Covers acceptance criteria A7 and the §9 currency semantics.</summary>
public class StorefrontConfigServiceTests
{
    [Fact]
    public async Task Get_Should_ServeAValidMinimalDocument_When_NothingIsConfigured()
    {
        // A7 — an unconfigured storefront gets defaults, never a 404.
        var (service, _, _) = NewService(tenantCurrency: null);

        var doc = await service.GetAsync();

        doc.Currency.Should().Be("GBP", "the last-resort fallback when the tenant record carries none");
        doc.RecommendedChoiceLabel.Should().Be("Recommended");
        doc.ResultsPageSize.Should().Be(8);
        doc.Delivery.Should().Be(new StorefrontDeliveryDto(0m, 0m));
        doc.DefaultBoxSlug.Should().BeNull();
        doc.Box.Should().BeNull("Spec 068 is not live; null is the defined state");
        doc.BackToTopTrigger.GetProperty("type").GetString().Should().Be("cardIndex");
        doc.BackToTopTrigger.GetProperty("value").GetInt32().Should().Be(10);
    }

    [Fact]
    public async Task Get_Should_LabelAmounts_WithTheCanonicalTenantCurrency()
    {
        // §9 — Tenant.DefaultCurrency, never a parallel setting that goes stale.
        var (service, _, _) = NewService(tenantCurrency: "NGN");

        (await service.GetAsync()).Currency.Should().Be("NGN");
    }

    [Fact]
    public async Task Update_Should_WriteSettings_AndTheRefreshedDocumentReflectsThem()
    {
        var (service, store, _) = NewService(tenantCurrency: "GBP");

        var doc = await service.UpdateAsync(new UpdateStorefrontConfigCommand(
            RecommendedChoiceLabel: "Abby's choice",
            ResultsPageSize: 12,
            DeliveryListAmount: 10m,
            DeliveryChargedAmount: 0m,
            DefaultBoxSlug: "abbys-box"));

        doc.RecommendedChoiceLabel.Should().Be("Abby's choice");
        doc.ResultsPageSize.Should().Be(12);
        doc.Delivery.Should().Be(new StorefrontDeliveryDto(10m, 0m));
        doc.DefaultBoxSlug.Should().Be("abbys-box");
        store.Values.Should().ContainKey(CommerceSettingNames.StorefrontResultsPageSize).WhoseValue.Should().Be("12");
    }

    [Fact]
    public async Task Update_Should_LeaveOmittedSettingsUnchanged_AndClearOnEmptyString()
    {
        var (service, store, _) = NewService(tenantCurrency: "GBP");
        await service.UpdateAsync(new UpdateStorefrontConfigCommand(RecommendedChoiceLabel: "Abby's choice", ResultsPageSize: 12));

        // Omitted members touch nothing; an explicit empty string clears back to the default.
        var doc = await service.UpdateAsync(new UpdateStorefrontConfigCommand(RecommendedChoiceLabel: ""));

        doc.RecommendedChoiceLabel.Should().Be("Recommended");
        doc.ResultsPageSize.Should().Be(12, "an omitted member must not disturb the stored value");
        store.Values.Should().NotContainKey(CommerceSettingNames.StorefrontRecommendedChoiceLabel);
    }

    [Fact]
    public async Task Update_Should_RejectInvalidInput_WithoutWritingAnything()
    {
        // §9 — the settings store has no cross-key transaction, so validation is front-loaded:
        // a rejected request leaves the document EXACTLY as it was, valid members included.
        var (service, store, _) = NewService(tenantCurrency: "GBP");

        var cases = new Func<Task>[]
        {
            () => service.UpdateAsync(new UpdateStorefrontConfigCommand(ResultsPageSize: 0)),
            () => service.UpdateAsync(new UpdateStorefrontConfigCommand(ResultsPageSize: 201)),
            () => service.UpdateAsync(new UpdateStorefrontConfigCommand(DeliveryListAmount: -1m)),
            () => service.UpdateAsync(new UpdateStorefrontConfigCommand(BackToTopTriggerJson: "[not object]")),
            () => service.UpdateAsync(new UpdateStorefrontConfigCommand(DefaultBoxSlug: "Not A Slug!")),
            // Valid label alongside an invalid size: the label must NOT land.
            () => service.UpdateAsync(new UpdateStorefrontConfigCommand(RecommendedChoiceLabel: "Sneaky", ResultsPageSize: 999)),
            // The store's own 4000-character value bound: a syntactically valid but oversized
            // trigger would otherwise let the label commit first, then 500 mid-document.
            () => service.UpdateAsync(new UpdateStorefrontConfigCommand(
                RecommendedChoiceLabel: "Sneaky",
                BackToTopTriggerJson: $$"""{"type":"cardIndex","note":"{{new string('n', 4200)}}"}""")),
        };

        foreach (var act in cases)
        {
            await act.Should().ThrowAsync<StorefrontValidationException>();
        }

        store.Values.Should().BeEmpty("a rejected request must write nothing at all");
    }

    // ─── Fakes ───────────────────────────────────────────────────────────────

    private static (StorefrontConfigService Service, FakeTenantSettingStore Store, Guid TenantId) NewService(string? tenantCurrency)
    {
        var tenantId = Guid.NewGuid();
        var store = new FakeTenantSettingStore();
        var service = new StorefrontConfigService(
            new FakeSettingProvider(),
            store,
            new FakeTenantCurrencyProvider(tenantCurrency),
            new TestTenantProvider(tenantId),
            new NoPlanBundleSizePlanService());
        return (service, store, tenantId);
    }

    /// <summary>No plan authored anywhere — the box section stays in its pre-068 null state,
    /// which is what every existing expectation in this class asserts.</summary>
    private sealed class NoPlanBundleSizePlanService : IBundleSizePlanService
    {
        public Task<BoxPlanDto> UpsertAsync(Guid productId, UpsertBundleSizePlanCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<BoxPlanDto?> GetForProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<BoxPlanDto?>(null);

        public Task<BoxPlanDto?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
            => Task.FromResult<BoxPlanDto?>(null);
    }

    /// <summary>Global/default chain only — mirrors the real provider's role in the composition:
    /// the tenant override lives in the store fake, the registered defaults come from here as
    /// null (the service applies its own §9 defaults on null).</summary>
    private sealed class FakeSettingProvider : ISettingProvider
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<string> GetRequiredAsync(string key, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException($"Setting '{key}' is required.");

        public Task<string?> GetForScopeAsync(string key, SettingScope scope, Guid? tenantId = null, Guid? userId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<SettingResolution> GetResolvedAsync(string key, Guid? tenantId = null, Guid? userId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new SettingResolution(key, null, "None"));
    }

    private sealed class FakeTenantSettingStore : ITenantSettingStore
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public Task<string?> GetTenantValueAsync(string key, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(Values.TryGetValue(key, out var value) ? value : (string?)null);

        public Task SetTenantValueAsync(string key, string? value, Guid tenantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Values.Remove(key);
            }
            else
            {
                Values[key] = value.Trim();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTenantCurrencyProvider(string? defaultCurrency) : ITenantCurrencyProvider
    {
        public Task<List<string>> GetTenantCurrencyCodesAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(defaultCurrency is null ? [] : new List<string> { defaultCurrency });

        public Task<string?> GetTenantDefaultCurrencyAsync(Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult(defaultCurrency);
    }
}
