using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class ApplyStatementImportEndpoint : EndpointWithoutRequest<StatementImportApplyResponse>
{
    private readonly IStatementImportService _statementImportService;

    public ApplyStatementImportEndpoint(IStatementImportService statementImportService)
    {
        _statementImportService = statementImportService;
    }

    public override void Configure()
    {
        Post("/personal-finance/imports/statements/{id}/apply");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var response = await _statementImportService.ApplyImportAsync(id, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}
