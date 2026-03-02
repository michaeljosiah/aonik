using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

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
