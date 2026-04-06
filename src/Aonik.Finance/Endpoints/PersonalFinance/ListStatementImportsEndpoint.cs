using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class ListStatementImportsEndpoint : EndpointWithoutRequest<IReadOnlyList<StatementImportResponse>>
{
    private readonly IStatementImportService _statementImportService;

    public ListStatementImportsEndpoint(IStatementImportService statementImportService)
    {
        _statementImportService = statementImportService;
    }

    public override void Configure()
    {
        Get("/personal-finance/imports/statements");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "List statement imports";
            s.Description = "Returns all bank statement imports for the authenticated user, including their processing status and row counts.";
            s.Response(200, "Statement imports returned successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _statementImportService.ListImportsAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
