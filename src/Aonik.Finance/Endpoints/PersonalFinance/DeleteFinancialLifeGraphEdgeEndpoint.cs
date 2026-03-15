using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class DeleteFinancialLifeGraphEdgeEndpoint : EndpointWithoutRequest
{
    private readonly FinancialLifeGraphWriteService _writeService;

    public DeleteFinancialLifeGraphEdgeEndpoint(FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Delete("/personal-finance/graph/edges/{id:guid}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await _writeService.DeleteEdgeAsync(Route<Guid>("id"), ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}
