using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Create a manual transaction";
            s.Description = "Records a new personal transaction manually, such as a cash expense or income entry not captured by bank imports.";
            s.Response(200, "Transaction created successfully");
            s.Response(401, "Not authenticated");
            s.Response(409, "Duplicate transaction detected");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
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
