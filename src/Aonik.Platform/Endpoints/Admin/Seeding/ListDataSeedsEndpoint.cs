using Aonik.Platform.Contracts.Api.Seeding;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "List available data seeds";
            s.Description = "Returns all registered global seed contributors with their key, display name, and description.";
            s.Response(200, "Available seeds");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("System Administration"));
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
