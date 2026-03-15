using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class CreateFinancialLifeGraphEdgeEndpoint : Endpoint<CreateFinancialLifeGraphEdgeRequest, FinancialLifeGraphEdgeWriteResponse>
{
    private readonly FinancialLifeGraphWriteService _writeService;

    public CreateFinancialLifeGraphEdgeEndpoint(FinancialLifeGraphWriteService writeService)
    {
        _writeService = writeService;
    }

    public override void Configure()
    {
        Post("/personal-finance/graph/edges");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CreateFinancialLifeGraphEdgeRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _writeService.CreateEdgeAsync(req, ct);
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
