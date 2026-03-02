using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class CreatePersonalTransactionEndpoint : Endpoint<CreateManualPersonalTransactionRequest, PersonalTransactionResponse>
{
    private readonly IPersonalTransactionService _personalTransactionService;

    public CreatePersonalTransactionEndpoint(IPersonalTransactionService personalTransactionService)
    {
        _personalTransactionService = personalTransactionService;
    }

    public override void Configure()
    {
        Post("/personal-finance/transactions");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CreateManualPersonalTransactionRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _personalTransactionService.CreateManualTransactionAsync(req, ct);
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
