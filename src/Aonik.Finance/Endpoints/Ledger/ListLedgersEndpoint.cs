using Aonik.Finance.Contracts.Api.Ledger;
using Aonik.Finance.Contracts.Services.Ledger;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await _ledgerService.ListLedgersAsync(ct);
        var response = result.Select(ledger => new LedgerResponse(ledger.Id, ledger.BaseCurrency, ledger.CreatedUtc)).ToList();
        await Send.OkAsync(response, ct);
    }
}
