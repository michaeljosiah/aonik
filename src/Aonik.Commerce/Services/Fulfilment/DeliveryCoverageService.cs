using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Commerce.Services.Fulfilment;

internal sealed class DeliveryCoverageService(
    ITenantSettingStore settings,
    ITenantProvider tenantProvider,
    IPostcodeLookup postcodes) : IDeliveryCoverageService
{
    private const int MaximumDocumentLength = 4000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public async Task<DeliveryCoverageDto> CheckAsync(string? postcode, CancellationToken cancellationToken = default)
    {
        var normalized = UkPostcode.Normalize(postcode)
            ?? throw new DeliveryCoverageException(DeliveryCoverageException.InvalidPostcode,
                "Enter a valid UK postcode.", "postcode");
        var configuration = await GetConfigurationAsync(cancellationToken);
        if (configuration is not { IsEnabled: true })
            return new(DeliveryCoverageStatuses.Unavailable, normalized);

        var result = await postcodes.LookupAsync(normalized, cancellationToken);
        if (result.Status == PostcodeLookupStatus.NotFound)
            throw new DeliveryCoverageException(DeliveryCoverageException.InvalidPostcode,
                "Enter a current UK postcode.", "postcode");

        // A lookup must confirm this postcode, never redirect the decision to a different address.
        if (result.Status != PostcodeLookupStatus.Found || UkPostcode.Normalize(result.Postcode) != normalized)
            return new(DeliveryCoverageStatuses.Unavailable, normalized);

        var outward = normalized[..normalized.IndexOf(' ')];
        var serves = configuration.AllowedOutwardCodes.Contains(outward, StringComparer.Ordinal)
            && !configuration.ExcludedOutwardCodes!.Contains(outward, StringComparer.Ordinal);
        // A calendar-only date is not a capacity promise. #346 supplies that integration.
        return new(serves ? DeliveryCoverageStatuses.Serves : DeliveryCoverageStatuses.NotServed, normalized);
    }

    public async Task<DeliveryCoverageConfigDto?> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var payload = await settings.GetTenantValueAsync(CommerceSettingNames.DeliveryCoverage,
            tenantProvider.GetCurrentTenantId(), cancellationToken);
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > MaximumDocumentLength) return null;
        try
        {
            var configuration = JsonSerializer.Deserialize<DeliveryCoverageConfigDto>(payload, JsonOptions);
            return configuration is null ? null : NormalizeConfiguration(configuration);
        }
        catch (JsonException) { return null; }
        catch (StorefrontValidationException) { return null; }
    }

    public async Task<DeliveryCoverageConfigDto> UpdateConfigurationAsync(DeliveryCoverageConfigDto configuration,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeConfiguration(configuration);
        var payload = JsonSerializer.Serialize(normalized, JsonOptions);
        if (payload.Length > MaximumDocumentLength)
            throw new StorefrontValidationException("Delivery coverage configuration exceeds the 4000-character settings limit.");
        await settings.SetTenantValueAsync(CommerceSettingNames.DeliveryCoverage, payload,
            tenantProvider.GetCurrentTenantId(), cancellationToken);
        return normalized;
    }

    private static DeliveryCoverageConfigDto NormalizeConfiguration(DeliveryCoverageConfigDto configuration)
    {
        if (configuration.AllowedOutwardCodes is null)
            throw new StorefrontValidationException("AllowedOutwardCodes must be supplied; an explicit empty array serves no postcodes.");
        var source = string.IsNullOrWhiteSpace(configuration.Source) ? null : configuration.Source.Trim();
        if (source is { Length: > 256 } || source?.Any(char.IsControl) == true)
            throw new StorefrontValidationException("Coverage source must be at most 256 characters without control characters.");
        return configuration with
        {
            AllowedOutwardCodes = NormalizeCodes(configuration.AllowedOutwardCodes),
            ExcludedOutwardCodes = NormalizeCodes(configuration.ExcludedOutwardCodes ?? []),
            Source = source
        };
    }

    private static string[] NormalizeCodes(IReadOnlyList<string> values)
    {
        if (values.Count > 1000)
            throw new StorefrontValidationException("Coverage rules exceed the settings document limit.");
        return values.Select(value => UkPostcode.NormalizeOutwardCode(value)
                ?? throw new StorefrontValidationException("Coverage rules must be exact UK outward codes, without wildcards."))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
