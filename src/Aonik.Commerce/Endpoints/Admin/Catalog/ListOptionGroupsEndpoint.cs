using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class ListOptionGroupsEndpoint : EndpointWithoutRequest<IReadOnlyList<OptionGroupDto>>
{
    private readonly IProductOptionService _options;

    public ListOptionGroupsEndpoint(IProductOptionService options) => _options = options;

    public override void Configure()
    {
        Get("/commerce/admin/option-groups");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List option groups, including inactive and half-authored ones.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _options.GetCatalogueAsync(includeInactive: true, ct), ct);
}
