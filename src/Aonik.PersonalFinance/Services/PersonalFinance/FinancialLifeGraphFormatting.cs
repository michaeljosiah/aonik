namespace Aonik.PersonalFinance.Services;

internal static class FinancialLifeGraphFormatting
{
    public static string BuildNodeId(string prefix, Guid id) => FinancialLifeGraphNodeKey.Create(prefix, id).ToString();

    public static string? SerializeMetadata(object? value) => FinancialLifeGraphMetadata.FromObject(value).Json;

    public static string? NormalizeMetadataJson(string? metadataJson) => FinancialLifeGraphMetadata.FromJson(metadataJson).Json;
}
