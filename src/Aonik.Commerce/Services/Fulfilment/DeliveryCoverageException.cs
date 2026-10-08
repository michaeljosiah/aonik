namespace Aonik.Commerce.Services.Fulfilment;

public sealed class DeliveryCoverageException(string code, string message, string? fieldName = null) : Exception(message)
{
    public const string InvalidPostcode = "commerce.invalid_postcode";
    public const string NotServed = "commerce.delivery_not_served";
    public const string Unavailable = "commerce.delivery_unavailable";
    public const string UnsupportedCountry = "commerce.unsupported_delivery_country";

    public string Code { get; } = code;
    public string? FieldName { get; } = fieldName;
}
