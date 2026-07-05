using Aonik.PersonalFinance.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class DeleteFinancialLifeGraphNodeEndpoint : EndpointWithoutRequest
{
    private readonly FinancialLifeGraphWriteService _writeService;

    public DeleteFinancialLifeGraphNodeEndpoint(FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Delete("/personal-finance/graph/nodes/{id:guid}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a graph node";
            s.Description = "Removes a node and its associated edges from the user's financial life graph.";
            s.Response(204, "Graph node deleted successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Node not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await _writeService.DeleteNodeAsync(Route<Guid>("id"), ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}
