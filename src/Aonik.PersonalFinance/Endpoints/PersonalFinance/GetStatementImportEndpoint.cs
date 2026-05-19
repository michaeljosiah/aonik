using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetStatementImportEndpoint : EndpointWithoutRequest<StatementImportResponse>
{
    private readonly IStatementImportService _statementImportService;

    public GetStatementImportEndpoint(IStatementImportService statementImportService)
    {
        _statementImportService = statementImportService;
    }

    public override void Configure()
    {
        Get("/personal-finance/imports/statements/{id}");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a statement import by ID";
            s.Description = "Returns the details and current status of a previously uploaded bank statement import.";
            s.Response(200, "Statement import returned successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Statement import not found");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var response = await _statementImportService.GetImportAsync(id, ct);

        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
