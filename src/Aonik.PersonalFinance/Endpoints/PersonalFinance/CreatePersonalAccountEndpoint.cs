using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class CreatePersonalAccountEndpoint : Endpoint<CreatePersonalAccountRequest, PersonalAccountResponse>
{
    private readonly IPersonalAccountService _personalAccountService;

    public CreatePersonalAccountEndpoint(IPersonalAccountService personalAccountService)
    {
        _personalAccountService = personalAccountService;
    }

    public override void Configure()
    {
        Post("/personal-finance/accounts");
        Policies("UserPolicy");
        Summary(s =>
        {
            s.Summary = "Create a personal account";
            s.Description = "Creates a new personal finance account such as a bank account, credit card, or cash wallet for tracking transactions.";
            s.Response(200, "Personal account created successfully");
            s.Response(401, "Not authenticated");
            s.Response(422, "Validation error");
        });
        Options(x => x.WithTags("Personal Finance"));
    }

    public override async Task HandleAsync(CreatePersonalAccountRequest req, CancellationToken ct)
    {
        try
        {
            var response = await _personalAccountService.CreateAccountAsync(req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
    }
}
