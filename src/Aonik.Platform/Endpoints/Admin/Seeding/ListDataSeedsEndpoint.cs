using Aonik.Platform.Contracts.Api.Seeding;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;

namespace Aonik.Platform.Endpoints.Admin.Seeding;

internal class ListDataSeedsEndpoint : EndpointWithoutRequest<DataSeedAvailableResponse>
{
    private readonly IEnumerable<IGlobalSeedContributor> _contributors;

    public ListDataSeedsEndpoint(IEnumerable<IGlobalSeedContributor> contributors)
    {
        _contributors = contributors;
    }

    public override void Configure()
    {
        Get("/admin/data-seeds");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var seeds = _contributors
            .OrderBy(c => c.SortOrder)
            .Select(c => new DataSeedInfo(c.Key, c.DisplayName, c.Description, c.SortOrder))
            .ToList();

        await Send.OkAsync(new DataSeedAvailableResponse(seeds), ct);
    }
}
