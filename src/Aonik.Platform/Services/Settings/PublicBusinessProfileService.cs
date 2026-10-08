using System.Text.Json;

using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;

namespace Aonik.Platform.Services.Settings;

internal sealed class PublicBusinessProfileService(
    ITenantSettingStore settings,
    ITenantProvider tenantProvider) : IPublicBusinessProfileService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public async Task<PublicBusinessProfileDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        // Administrative tenant contacts and global settings are never publication fallbacks.
        var payload = await settings.GetTenantValueAsync(
            BusinessProfileSettingNames.Profile, tenantProvider.GetCurrentTenantId(), cancellationToken);
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > 4000) return null;

        try
        {
            var document = JsonSerializer.Deserialize<PublicBusinessProfileDocument>(payload, SerializerOptions);
            if (document is not { IsPublished: true, Profile: not null }) return null;

            var profile = document.Profile;
            if (string.IsNullOrWhiteSpace(profile.DisplayName)
                || !IsPublicUrl(profile.Website) || !IsPublicUrl(profile.LogoUrl)
                || !HasValidHours(profile.OpeningHours)) return null;

            return profile with { DisplayName = profile.DisplayName.Trim() };
        }
        catch (JsonException)
        {
            // Generic settings can contain an invalid document. Do not publish partial facts.
            return null;
        }
    }

    private static bool IsPublicUrl(string? value)
        => value is null || (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));

    private static bool HasValidHours(BusinessOpeningHoursDto? hours)
    {
        if (hours is null) return true;
        if (string.IsNullOrWhiteSpace(hours.Timezone)
            || !TimeZoneInfo.TryConvertIanaIdToWindowsId(hours.Timezone, out _)
            || hours.WeeklyHours is null || hours.BankHolidays is null || hours.ExceptionalClosures is null)
            return false;

        if (hours.WeeklyHours.Any(period => period is null || period.DayOfWeek is < 1 or > 7
            || period.OpensAt >= period.ClosesAt)) return false;

        foreach (var day in hours.WeeklyHours.GroupBy(period => period.DayOfWeek))
        {
            var periods = day.OrderBy(period => period.OpensAt).ToArray();
            for (var index = 1; index < periods.Length; index++)
            {
                if (periods[index].OpensAt < periods[index - 1].ClosesAt) return false;
            }
        }

        return true;
    }
}
