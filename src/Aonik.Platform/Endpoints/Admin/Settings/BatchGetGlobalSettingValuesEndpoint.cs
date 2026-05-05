using Aonik.Platform.Contracts.Api.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.SharedKernel.Abstractions.Settings;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Settings;

internal class BatchGetGlobalSettingValuesEndpoint
    : Endpoint<BatchGetSettingValuesRequest, BatchGetSettingValuesResponse>
{
    private readonly ISettingProvider _settingProvider;

    public BatchGetGlobalSettingValuesEndpoint(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public override void Configure()
    {
        Post("/admin/settings/values/batch");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Get multiple global setting values";
            s.Description = "Retrieves the current values of multiple global platform settings by their keys in a single request.";
            s.Response(200, "Setting values");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(BatchGetSettingValuesRequest req, CancellationToken ct)
    {
        var results = new List<SettingValueResponse>();

        foreach (var key in req.Keys)
        {
            if (SettingDefinitions.Get(key) == null)
                continue;

            var value = await _settingProvider.GetForScopeAsync(key, SettingScope.Global, cancellationToken: ct);
            results.Add(new SettingValueResponse(key, value, "Global"));
        }

        await Send.OkAsync(new BatchGetSettingValuesResponse(results), ct);
    }
}
