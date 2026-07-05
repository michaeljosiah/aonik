using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class CreateFinancialLifeGraphEdgeEndpoint : Endpoint<CreateFinancialLifeGraphEdgeRequest, FinancialLifeGraphEdgeWriteResponse>
{
    private readonly FinancialLifeGraphWriteService _writeService;

    public CreateFinancialLifeGraphEdgeEndpoint(FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Post("/personal-finance/graph/edges");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a graph edge";
            s.Description = "Adds a new edge (relationship or connection) between two nodes in the user's financial life graph.";
            s.Response(200, "Graph edge created successfully");
            s.Response(401, "Not authenticated");
            s.Response(409, "Duplicate edge already exists");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreateFinancialLifeGraphEdgeRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _writeService.CreateEdgeAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}
