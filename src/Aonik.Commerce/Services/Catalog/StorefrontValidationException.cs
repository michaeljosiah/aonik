namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// Invalid storefront/merchandising input — an unknown facet key, an option label submitted where
/// a value token belongs, a sort that needs a collection, malformed authoring JSON (Spec 070
/// §6/§11). Mapped to HTTP 400: "a storefront bug should be loud", never silently ignored — and
/// never a 500, which is what an unmapped exception type becomes.
/// </summary>
public sealed class StorefrontValidationException : Exception
{
    public StorefrontValidationException(string message)
        : base(message)
    {
    }
}
