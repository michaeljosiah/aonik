namespace Aonik.Commerce.Contracts.Models.Fulfilment;

public enum PostcodeLookupStatus
{
    Found,
    NotFound,
    Unavailable
}

public record PostcodeLookupResult(PostcodeLookupStatus Status, string? Postcode = null);
