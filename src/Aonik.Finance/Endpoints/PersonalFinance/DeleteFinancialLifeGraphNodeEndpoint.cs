using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class DeleteFinancialLifeGraphNodeEndpoint : EndpointWithoutRequest
{
    private readonly FinancialLifeGraphWriteService _writeService;

    public DeleteFinancialLifeGraphNodeEndpoint(FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Delete("/personal-finance/graph/nodes/{id:guid}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            await _writeService.DeleteNodeAsync(Route<Guid>("id"), ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 404);
        }
    }
}
