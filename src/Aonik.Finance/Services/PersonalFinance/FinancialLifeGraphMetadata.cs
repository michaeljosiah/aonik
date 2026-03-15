using System.Text.Json;

namespace Aonik.Finance.Services.PersonalFinance;

internal readonly record struct FinancialLifeGraphMetadata(string? Json)
{
    public static FinancialLifeGraphMetadata FromObject(object? value)
    {
        if (value is null)
        {
            return new FinancialLifeGraphMetadata(null);
        }

        return FromJson(JsonSerializer.Serialize(value));
    }

    public static FinancialLifeGraphMetadata FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new FinancialLifeGraphMetadata(null);
        }

        var normalized = json.Trim();
        return normalized is "null" or "{}"
            ? new FinancialLifeGraphMetadata(null)
            : new FinancialLifeGraphMetadata(normalized);
    }
}
