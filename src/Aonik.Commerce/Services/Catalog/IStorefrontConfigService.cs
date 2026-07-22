using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>The Spec 070 §9 storefront-config document: one anonymous, cacheable read of every
/// tunable the frontend must not hard-code, and the Commerce-owned write behind it.</summary>
public interface IStorefrontConfigService
{
    /// <summary>Composes the document from tenant settings and the tenant's canonical currency.
    /// Unset values fall back to registered defaults — never a 404.</summary>
    Task<StorefrontConfigDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the Commerce.Storefront.* settings (null = unchanged, empty = clear the
    /// tenant override). Validated before anything persists — a partial write must not leave the
    /// document half-updated.</summary>
    Task<StorefrontConfigDto> UpdateAsync(UpdateStorefrontConfigCommand command, CancellationToken cancellationToken = default);
}
