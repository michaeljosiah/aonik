using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Commerce.Services.Checkout;

internal static class SaleTermsPolicy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { RespectRequiredConstructorParameters = true };

    public static async Task<SaleTermsDto?> ReadAsync(ITenantSettingStore settings, Guid tenantId,
        CancellationToken cancellationToken = default) => Parse(await settings.GetTenantValueAsync(
            CommerceSettingNames.StorefrontSaleTerms, tenantId, cancellationToken));

    public static async Task<AcceptedSaleTermsDto?> AcceptAsync(ITenantSettingStore settings, Guid tenantId,
        string? acceptedVersion, DateTime acceptedAtUtc, CancellationToken cancellationToken = default)
    {
        var configured = await settings.GetTenantValueAsync(CommerceSettingNames.StorefrontSaleTerms, tenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (acceptedVersion is not null)
                throw new StorefrontValidationException("AcceptedTermsVersion: the terms are unavailable. Refresh before checkout.");
            return null;
        }
        var terms = Parse(configured)
            ?? throw new StorefrontValidationException("AcceptedTermsVersion: the terms are unavailable. Please try again later.");
        if (!string.Equals(acceptedVersion, terms.Version, StringComparison.Ordinal))
            throw new StorefrontValidationException("AcceptedTermsVersion: read and accept the current Terms of Sale before checkout.");
        return new(terms.Version, terms.Url, acceptedAtUtc);
    }

    private static SaleTermsDto? Parse(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured) || configured.Length > 4000) return null;
        SaleTermsDto? terms;
        try { terms = JsonSerializer.Deserialize<SaleTermsDto>(configured, Json); }
        catch (JsonException) { return null; }
        if (terms is null || string.IsNullOrWhiteSpace(terms.Version) || terms.Version.Length > 128
            || terms.Version != terms.Version.Trim() || terms.Version.Any(char.IsControl)
            || terms.Url is not { Length: > 0 and <= 2048 } || terms.Url.Any(char.IsControl)
            || !Uri.TryCreate(terms.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)) return null;
        return terms;
    }
}
