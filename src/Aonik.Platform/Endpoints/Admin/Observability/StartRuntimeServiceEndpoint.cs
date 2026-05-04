using Aonik.Platform.Contracts.Api.Observability;
using Aonik.Platform.Contracts.Services.Operations;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Platform.Endpoints.Admin.Observability;

internal sealed class StartRuntimeServiceEndpoint : EndpointWithoutRequest<RuntimeServiceActionResponse>
{
    private readonly IRuntimeOperationsService _runtimeOperationsService;

    public StartRuntimeServiceEndpoint(IRuntimeOperationsService runtimeOperationsService)
    {
        _runtimeOperationsService = runtimeOperationsService;
    }

    public override void Configure()
    {
        Post("/admin/observability/runtime/services/{serviceName}/start");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Start a runtime service";
            s.Description = "Requests a dev-environment Azure Container App to start by increasing its minimum replica count so operators can wake scaled-to-zero services from the topology surface.";
            s.Response(200, "Start request accepted");
            s.Response(401, "Not authenticated");
            s.Response(404, "Service not found");
        });
        Options(x => x.WithTags("Observability"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var serviceName = Route<string>("serviceName")!;
        var result = await _runtimeOperationsService.StartServiceAsync(serviceName, ct);

        if (!result.Success && result.Runtime is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}
