using FastEndpoints;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record ValidateWorkflowRequest(string Slug);

internal sealed class ValidateWorkflowEndpoint(IWorkflowService service) : Endpoint<ValidateWorkflowRequest, WorkflowValidation>
{
    public override void Configure()
    {
        Post("/ai/workflows/{Slug}/validate");
    }

    public override async Task HandleAsync(ValidateWorkflowRequest req, CancellationToken ct)
    {
        WorkflowValidation? result = await service.ValidateAsync(Route<string>("Slug")!, ct);
        if (result is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(result, ct);
    }
}
