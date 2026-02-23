using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Pricing;

internal class CurrencyMetadataProvider : ICurrencyMetadataProvider
{
    private static readonly IReadOnlyDictionary<string, CurrencyMetadata> CurrencyMap =
        new Dictionary<string, CurrencyMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = new CurrencyMetadata("USD", 2),
            ["EUR"] = new CurrencyMetadata("EUR", 2),
            ["GBP"] = new CurrencyMetadata("GBP", 2),
            ["JPY"] = new CurrencyMetadata("JPY", 0),
            ["CAD"] = new CurrencyMetadata("CAD", 2),
            ["AUD"] = new CurrencyMetadata("AUD", 2),
            ["CHF"] = new CurrencyMetadata("CHF", 2),
            ["CNY"] = new CurrencyMetadata("CNY", 2),
            ["SEK"] = new CurrencyMetadata("SEK", 2),
            ["NZD"] = new CurrencyMetadata("NZD", 2),
            ["KES"] = new CurrencyMetadata("KES", 0),
            ["NGN"] = new CurrencyMetadata("NGN", 2),
            ["GHS"] = new CurrencyMetadata("GHS", 2),
            ["UGX"] = new CurrencyMetadata("UGX", 0),
            ["TZS"] = new CurrencyMetadata("TZS", 0),
            ["ZAR"] = new CurrencyMetadata("ZAR", 2)
        };

    public bool TryGetCurrency(string currency, out CurrencyMetadata metadata)
        => CurrencyMap.TryGetValue(currency, out metadata!);

    public CurrencyMetadata GetCurrency(string currency)
    {
        if (!TryGetCurrency(currency, out var metadata))
        {
            throw new ArgumentException($"Unsupported currency: {currency}", nameof(currency));
        }

        return metadata;
    }
}
