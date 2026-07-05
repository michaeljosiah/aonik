using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class ListStatementImportRowsEndpoint : EndpointWithoutRequest<IReadOnlyList<StatementImportRowResponse>>
{
    private readonly IStatementImportService _statementImportService;

    public ListStatementImportRowsEndpoint(IStatementImportService statementImportService)
    {
        _statementImportService = statementImportService;
    }

    public override void Configure()
    {
        Get("/personal-finance/imports/statements/{id}/rows");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List statement import rows";
            s.Description = "Returns all parsed rows from a specific statement import, showing each transaction line before it is applied.";
            s.Response(200, "Import rows returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Statement import not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var response = await _statementImportService.ListImportRowsAsync(id, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
