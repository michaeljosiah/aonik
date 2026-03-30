using Aonik.Platform.Contracts.Api.Seeding;
using Aonik.SharedKernel.Abstractions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Aonik.Platform.Endpoints.Admin.Seeding;

internal class RunDataSeedEndpoint : Endpoint<DataSeedRequest, DataSeedResponse>
{
    private readonly IEnumerable<IGlobalSeedContributor> _contributors;
    private readonly ILogger<RunDataSeedEndpoint> _logger;

    public RunDataSeedEndpoint(
        IEnumerable<IGlobalSeedContributor> contributors,
        ILogger<RunDataSeedEndpoint> logger)
    {
        _contributors = contributors;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/admin/data-seeds/run");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(DataSeedRequest req, CancellationToken ct)
    {
        var contributors = _contributors.OrderBy(c => c.SortOrder).ToList();

        // If specific keys are provided, filter to just those
        if (req.Keys is { Count: > 0 })
        {
            var requestedKeys = new HashSet<string>(req.Keys, StringComparer.OrdinalIgnoreCase);
            contributors = contributors.Where(c => requestedKeys.Contains(c.Key)).ToList();
        }

        if (contributors.Count == 0)
        {
            await Send.OkAsync(new DataSeedResponse(DateTime.UtcNow, []), ct);
            return;
        }

        var results = new List<DataSeedResultItem>();

        foreach (var contributor in contributors)
        {
            _logger.LogInformation("Running data seed: {Key} ({DisplayName})", contributor.Key, contributor.DisplayName);

            try
            {
                var operations = await contributor.SeedAsync(ct);
                results.Add(new DataSeedResultItem(contributor.Key, contributor.DisplayName, operations));

                _logger.LogInformation("Data seed completed: {Key}", contributor.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data seed failed: {Key}", contributor.Key);
                results.Add(new DataSeedResultItem(
                    contributor.Key,
                    contributor.DisplayName,
                    [$"Failed: {ex.Message}"]));
            }
        }

        await Send.OkAsync(new DataSeedResponse(DateTime.UtcNow, results), ct);
    }
}
