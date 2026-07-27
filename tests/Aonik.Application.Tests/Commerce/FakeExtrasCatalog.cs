using Aonik.Commerce.Services.Catalog;

namespace Aonik.Application.Tests.Commerce;

/// <summary>Empty-rail stand-in for tests that construct <see cref="CollectionService"/>
/// but never touch the extras enrichment: no rows, no skips, default slug.</summary>
internal sealed class FakeExtrasCatalog : IExtrasCatalogService
{
    public Task<ExtrasListDto> GetExtrasAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ExtrasListDto([], 0));

    public Task<string> GetConfiguredSlugAsync(CancellationToken cancellationToken = default)
        => Task.FromResult("extras");
}
