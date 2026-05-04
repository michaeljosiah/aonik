using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal sealed class GetRuntimeServicesEndpoint : EndpointWithoutRequest<IReadOnlyList<RuntimeServiceStatus>>
{
    private readonly IRuntimeOperationsService _runtimeOperationsService;

    public GetRuntimeServicesEndpoint(IRuntimeOperationsService runtimeOperationsService)
    {
        _runtimeOperationsService = runtimeOperationsService;
    }

    public override void Configure()
    {
        Get("/admin/observability/runtime/services");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "List runtime service states";
            s.Description = "Returns live Azure Container Apps runtime state for platform services so operators can see which services are running, scaled to zero, or degraded.";
            s.Response(200, "Runtime service states");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _runtimeOperationsService.ListRuntimeServicesAsync(ct);
        await Send.OkAsync(result, ct);
    }
}
