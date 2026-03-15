using System.Text.Json;

namespace Aonik.Finance.Services.PersonalFinance;

internal static class FinancialLifeGraphFormatting
{
    public static string BuildNodeId(string prefix, Guid id) => $"{prefix}:{id:D}";

    public static string? SerializeMetadata(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return NormalizeMetadataJson(JsonSerializer.Serialize(value));
    }

    public static string? NormalizeMetadataJson(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        var normalized = metadataJson.Trim();
        return normalized is "null" or "{}" ? null : normalized;
    }
}
