using Aonik.Commerce.Contracts.Models.Fulfilment;

namespace Aonik.Commerce.Services.Fulfilment;

/// <summary>Looks up current postcode existence, independently of courier coverage.</summary>
public interface IPostcodeLookup
{
    Task<PostcodeLookupResult> LookupAsync(
        string normalisedPostcode, CancellationToken cancellationToken = default);
}
