using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class CreateFinancialLifeGraphNodeEndpoint : Endpoint<CreateFinancialLifeGraphNodeRequest, FinancialLifeGraphNodeWriteResponse>
{
    private readonly FinancialLifeGraphWriteService _writeService;

    public CreateFinancialLifeGraphNodeEndpoint(FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Post("/personal-finance/graph/nodes");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CreateFinancialLifeGraphNodeRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _writeService.CreateNodeAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}
