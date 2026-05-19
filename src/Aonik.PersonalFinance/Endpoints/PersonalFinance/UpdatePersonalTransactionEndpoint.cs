using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Update a personal transaction";
            s.Description = "Partially updates a manually created personal transaction's details such as amount, category, or description.";
            s.Response(200, "Transaction updated successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Transaction not found");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
