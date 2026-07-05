using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

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
        Summary(s =>
        {
            s.Summary = "Apply a statement import";
            s.Description = "Finalises a previously uploaded statement import by creating personal transactions from the imported rows.";
            s.Response(200, "Statement import applied successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Import cannot be applied in its current state");
        });
        Options(x => x.WithTags("Personal Finance"));
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
