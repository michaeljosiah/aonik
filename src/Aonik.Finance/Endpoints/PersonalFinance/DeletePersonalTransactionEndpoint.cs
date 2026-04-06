using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Delete a personal transaction";
            s.Description = "Permanently deletes a manually created personal transaction. Imported transactions cannot be deleted.";
            s.Response(204, "Transaction deleted successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Transaction not found");
            s.Response(422, "Transaction cannot be deleted (e.g. imported transaction)");
        });
        Options(x => x.WithTags("Personal Finance"));
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
