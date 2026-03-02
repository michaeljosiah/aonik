using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _statementImportService.ListImportsAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
