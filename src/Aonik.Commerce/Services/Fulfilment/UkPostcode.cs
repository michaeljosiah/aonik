using System.Text.RegularExpressions;

namespace Aonik.Commerce.Services.Fulfilment;

/// <summary>Bounded input syntax only. A provider must establish that a postcode currently exists.</summary>
internal static partial class UkPostcode
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 16) return null;
        var compact = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (!FullPattern().IsMatch(compact)) return null;
        return compact.Insert(compact.Length - 3, " ");
    }

    public static string? NormalizeOutwardCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 8) return null;
        var normalized = value.Trim().ToUpperInvariant();
        return OutwardPattern().IsMatch(normalized) ? normalized : null;
    }

    [GeneratedRegex(@"\A(?:[A-Z]{1,2}[0-9][A-Z0-9]?|GIR)[0-9][A-Z]{2}\z", RegexOptions.CultureInvariant)]
    private static partial Regex FullPattern();

    [GeneratedRegex(@"\A(?:[A-Z]{1,2}[0-9][A-Z0-9]?|GIR)\z", RegexOptions.CultureInvariant)]
    private static partial Regex OutwardPattern();
}
