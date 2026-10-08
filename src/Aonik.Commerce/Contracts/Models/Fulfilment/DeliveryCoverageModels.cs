using System.Text.Json.Serialization;

namespace Aonik.Commerce.Contracts.Models.Fulfilment;

public record DeliveryCoverageDto(string Status, string? NormalisedPostcode, DateOnly? EarliestDate = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record DeliveryCoverageConfigDto(
    [property: JsonRequired] bool IsEnabled,
    [property: JsonRequired] IReadOnlyList<string> AllowedOutwardCodes,
    IReadOnlyList<string>? ExcludedOutwardCodes = null,
    string? Source = null);

public static class DeliveryCoverageStatuses
{
    public const string Serves = "serves";
    public const string NotServed = "not_served";
    public const string Unavailable = "unavailable";
}
