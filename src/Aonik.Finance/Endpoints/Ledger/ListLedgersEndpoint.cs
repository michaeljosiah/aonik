using Aonik.Finance.Contracts.Api.Ledger;
using Aonik.Finance.Contracts.Services.Ledger;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Ledger;

public class ListLedgersEndpoint : EndpointWithoutRequest<List<LedgerResponse>>
{
    private readonly ILedgerService _ledgerService;

    public ListLedgersEndpoint(ILedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    public override void Configure()
    {
        Get("/ledger");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List all ledgers";
            s.Description = "Returns all ledgers for the current tenant, including base currency and creation date.";
            s.Response(200, "Ledgers retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Ledger"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _ledgerService.ListLedgersAsync(ct);
        var response = result.Select(ledger => new LedgerResponse(ledger.Id, ledger.BaseCurrency, ledger.CreatedUtc)).ToList();
        await Send.OkAsync(response, ct);
    }
}
