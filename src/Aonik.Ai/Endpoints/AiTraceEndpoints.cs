using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Ai.Endpoints;

internal sealed class ListAiTracesEndpoint : Endpoint<ListAiTracesRequest, ListAiTracesResponse>
{
    private readonly AiTraceQueryService _service;

    public ListAiTracesEndpoint(AiTraceQueryService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/traces");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List AI traces";
            s.Description = "Returns paginated AI runs enriched with correlated Application Insights telemetry when available.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(ListAiTracesRequest req, CancellationToken ct)
    {
        var result = await _service.ListAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}

internal sealed class GetAiTraceEndpoint : Endpoint<GetAiTraceRequest, AiTraceRunDetailResponse>
{
    private readonly AiTraceQueryService _service;

    public GetAiTraceEndpoint(AiTraceQueryService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/traces/{RunId}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get AI trace by run ID";
            s.Description = "Returns a single AI run with merged audit data and raw Application Insights telemetry.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Trace not found");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(GetAiTraceRequest req, CancellationToken ct)
    {
        var result = await _service.GetAsync(req.RunId, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(result, ct);
    }
}

internal sealed class ListAiTraceObservationsEndpoint : Endpoint<ListAiTraceObservationsRequest, ListAiTraceObservationsResponse>
{
    private readonly AiTraceExplorerService _service;

    public ListAiTraceObservationsEndpoint(AiTraceExplorerService service) => _service = service;

    public override void Configure()
    {
        Get("/ai/trace-observations");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List AI trace observations";
            s.Description = "Returns provider-neutral AI trace observations from Langfuse or Application Insights.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Configuration"));
    }

    public override async Task HandleAsync(ListAiTraceObservationsRequest req, CancellationToken ct)
    {
        var result = await _service.ListObservationsAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
