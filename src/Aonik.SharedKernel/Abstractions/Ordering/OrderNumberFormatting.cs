using System.Globalization;

namespace Aonik.SharedKernel.Abstractions.Ordering;

public static class OrderNumberFormatting
{
    /// <summary>Compatibility reference for tenants that have not authored an Order profile.</summary>
    public static string CreateFallback(DateTime utcNow)
    {
        var timestamp = utcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var token = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"ORD-{timestamp}-{token}";
    }
}
