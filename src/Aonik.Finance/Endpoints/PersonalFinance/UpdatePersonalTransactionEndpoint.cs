using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class UpdatePersonalTransactionEndpoint : Endpoint<UpdateManualPersonalTransactionRequest, PersonalTransactionResponse>
{
    private readonly IPersonalTransactionService _personalTransactionService;

    public UpdatePersonalTransactionEndpoint(IPersonalTransactionService personalTransactionService)
    {
        _personalTransactionService = personalTransactionService;
    }

    public override void Configure()
    {
        Patch("/personal-finance/transactions/{id}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(UpdateManualPersonalTransactionRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var response = await _personalTransactionService.UpdateManualTransactionAsync(id, req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
