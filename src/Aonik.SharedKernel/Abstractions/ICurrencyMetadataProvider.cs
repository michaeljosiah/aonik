namespace Aonik.SharedKernel.Abstractions;

public interface ICurrencyMetadataProvider
{
    bool TryGetCurrency(string currency, out CurrencyMetadata metadata);
    CurrencyMetadata GetCurrency(string currency);
}

public record CurrencyMetadata(string Code, int DecimalPlaces);
