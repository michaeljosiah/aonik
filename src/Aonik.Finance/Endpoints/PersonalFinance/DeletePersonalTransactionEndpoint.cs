using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class DeletePersonalTransactionEndpoint : EndpointWithoutRequest
{
    private readonly IPersonalTransactionService _personalTransactionService;

    public DeletePersonalTransactionEndpoint(IPersonalTransactionService personalTransactionService)
    {
        _personalTransactionService = personalTransactionService;
    }

    public override void Configure()
    {
        Delete("/personal-finance/transactions/{id}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            await _personalTransactionService.DeleteManualTransactionAsync(id, ct);
            await Send.NoContentAsync(ct);
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
