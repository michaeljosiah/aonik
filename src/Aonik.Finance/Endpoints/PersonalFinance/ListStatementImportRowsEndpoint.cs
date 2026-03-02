using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
