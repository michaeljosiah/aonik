using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class DeleteFinancialLifeGraphEdgeEndpoint : EndpointWithoutRequest
{
    private readonly FinancialLifeGraphWriteService _writeService;

    public DeleteFinancialLifeGraphEdgeEndpoint(FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Delete("/personal-finance/graph/edges/{id:guid}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Delete a graph edge";
            s.Description = "Removes a relationship edge from the user's financial life graph.";
            s.Response(204, "Graph edge deleted successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Edge not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await _writeService.DeleteEdgeAsync(Route<Guid>("id"), ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}
