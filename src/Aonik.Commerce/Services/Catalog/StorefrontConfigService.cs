using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Composes and writes the Spec 070 §9 storefront-config document.</summary>
internal sealed partial class StorefrontConfigService : IStorefrontConfigService
{
    private const string FallbackCurrency = "GBP";
    private const int DefaultPageSize = 8;
    private const string DefaultRecommendedLabel = "Recommended";
    private const string DefaultBackToTopTriggerJson = """{"type":"cardIndex","value":10}""";

    private readonly ISettingProvider _settings;
    private readonly ITenantSettingStore _settingStore;
    private readonly ITenantCurrencyProvider _tenantCurrency;
    private readonly ITenantProvider _tenantProvider;

    public StorefrontConfigService(
        ISettingProvider settings,
        ITenantSettingStore settingStore,
        ITenantCurrencyProvider tenantCurrency,
        ITenantProvider tenantProvider)
    {
        _settings = settings;
        _settingStore = settingStore;
        _tenantCurrency = tenantCurrency;
        _tenantProvider = tenantProvider;
    }

    public async Task<StorefrontConfigDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // The canonical tenant currency — never a parallel setting that goes stale the day the
        // tenant's currency changes (§9). GBP only when the tenant record carries none.
        var currency = await _tenantCurrency.GetTenantDefaultCurrencyAsync(tenantId, cancellationToken)
            ?? FallbackCurrency;

        var label = await ReadTenantSettingAsync(CommerceSettingNames.StorefrontRecommendedChoiceLabel, tenantId, cancellationToken)
            ?? DefaultRecommendedLabel;

        var pageSizeRaw = await ReadTenantSettingAsync(CommerceSettingNames.StorefrontResultsPageSize, tenantId, cancellationToken);
        var pageSize = int.TryParse(pageSizeRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSize)
            && parsedSize is >= 1 and <= 200
                ? parsedSize
                : DefaultPageSize;

        var triggerJson = await ReadTenantSettingAsync(CommerceSettingNames.StorefrontBackToTopTriggerJson, tenantId, cancellationToken);
        var trigger = ParseTrigger(triggerJson) ?? ParseTrigger(DefaultBackToTopTriggerJson)!.Value;

        var delivery = new StorefrontDeliveryDto(
            await ReadAmountAsync(CommerceSettingNames.StorefrontDeliveryListAmount, tenantId, cancellationToken),
            await ReadAmountAsync(CommerceSettingNames.StorefrontDeliveryChargedAmount, tenantId, cancellationToken));

        var boxSlug = await ReadTenantSettingAsync(CommerceSettingNames.StorefrontDefaultBoxProductSlug, tenantId, cancellationToken);
        boxSlug = string.IsNullOrWhiteSpace(boxSlug) ? null : boxSlug.Trim();

        // Box: the default bundle's Spec 068 size plan. 068 is not live yet, so there is no plan
        // to read and the section is null — exactly the "not yet live" state §9 defines. When 068
        // lands, its plan read plugs in HERE and nowhere else; the frontend contract is already
        // shaped for it.
        StorefrontBoxPlanDto? box = null;

        return new StorefrontConfigDto(currency, label, pageSize, trigger, delivery, boxSlug, box);
    }

    public async Task<StorefrontConfigDto> UpdateAsync(
        UpdateStorefrontConfigCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Validate EVERYTHING before writing ANYTHING — the settings store has no transaction
        // spanning keys, so the only way a failed request leaves the document unchanged is to
        // front-load every rejection.
        if (command.ResultsPageSize is { } size and (< 1 or > 200))
        {
            throw new StorefrontValidationException($"resultsPageSize must be between 1 and 200; got {size}.");
        }

        if (command.DeliveryListAmount is < 0 || command.DeliveryChargedAmount is < 0)
        {
            throw new StorefrontValidationException("Delivery amounts cannot be negative.");
        }

        if (command.BackToTopTriggerJson is { } triggerJson
            && !string.IsNullOrWhiteSpace(triggerJson)
            && ParseTrigger(triggerJson) is null)
        {
            throw new StorefrontValidationException("backToTopTriggerJson must be a JSON object.");
        }

        if (command.DefaultBoxSlug is { } slug && slug.Length > 0 && !SlugPattern().IsMatch(slug.Trim().ToLowerInvariant()))
        {
            throw new StorefrontValidationException(
                $"'{slug}' is not a valid product slug; use 1-160 characters of a-z, 0-9 or '-'.");
        }

        // Null = unchanged; empty string = clear the tenant override (the writer treats
        // whitespace as removal, falling back to the registered default).
        if (command.RecommendedChoiceLabel is { } label)
        {
            await _settingStore.SetTenantValueAsync(
                CommerceSettingNames.StorefrontRecommendedChoiceLabel, label, tenantId, cancellationToken);
        }

        if (command.ResultsPageSize is { } pageSize)
        {
            await _settingStore.SetTenantValueAsync(
                CommerceSettingNames.StorefrontResultsPageSize,
                pageSize.ToString(CultureInfo.InvariantCulture), tenantId, cancellationToken);
        }

        if (command.BackToTopTriggerJson is { } trigger)
        {
            await _settingStore.SetTenantValueAsync(
                CommerceSettingNames.StorefrontBackToTopTriggerJson, trigger, tenantId, cancellationToken);
        }

        if (command.DeliveryListAmount is { } listAmount)
        {
            await _settingStore.SetTenantValueAsync(
                CommerceSettingNames.StorefrontDeliveryListAmount,
                listAmount.ToString(CultureInfo.InvariantCulture), tenantId, cancellationToken);
        }

        if (command.DeliveryChargedAmount is { } chargedAmount)
        {
            await _settingStore.SetTenantValueAsync(
                CommerceSettingNames.StorefrontDeliveryChargedAmount,
                chargedAmount.ToString(CultureInfo.InvariantCulture), tenantId, cancellationToken);
        }

        if (command.DefaultBoxSlug is { } boxSlug)
        {
            await _settingStore.SetTenantValueAsync(
                CommerceSettingNames.StorefrontDefaultBoxProductSlug,
                boxSlug.Trim().ToLowerInvariant(), tenantId, cancellationToken);
        }

        return await GetAsync(cancellationToken);
    }

    /// <summary>Tenant override first (via the module-owned store — the provider's scoped read
    /// enforces the platform Settings.Read permission, which an Operations admin legitimately
    /// lacks), then the provider's Global → configuration → registered-default chain.</summary>
    private async Task<string?> ReadTenantSettingAsync(string key, Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantValue = await _settingStore.GetTenantValueAsync(key, tenantId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(tenantValue))
        {
            return tenantValue;
        }

        return await _settings.GetAsync(key, cancellationToken);
    }

    private async Task<decimal> ReadAmountAsync(string key, Guid tenantId, CancellationToken cancellationToken)
    {
        var raw = await ReadTenantSettingAsync(key, tenantId, cancellationToken);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount >= 0
            ? amount
            : 0m;
    }

    private static JsonElement? ParseTrigger(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex("^[a-z0-9-]{1,160}$")]
    private static partial Regex SlugPattern();
}
