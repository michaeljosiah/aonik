using System.Text.Json;

namespace Aonik.Commerce.Services.Promotions;

internal static class DiscountAllocationSnapshot
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { RespectRequiredConstructorParameters = true };

    public static string Serialize(IReadOnlyList<OrderDiscountAllocation> allocations)
    {
        var json = JsonSerializer.Serialize(allocations, Json);
        if (json.Length > 262144) throw new InvalidOperationException("Discount allocation snapshot is too large.");
        return json;
    }

    public static IReadOnlyList<OrderDiscountAllocation> Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<OrderDiscountAllocation[]>(json, Json)
                ?? throw new InvalidOperationException("Discount allocation snapshot is invalid.");
        }
        catch (JsonException error) { throw new InvalidOperationException("Discount allocation snapshot is invalid.", error); }
    }
}
