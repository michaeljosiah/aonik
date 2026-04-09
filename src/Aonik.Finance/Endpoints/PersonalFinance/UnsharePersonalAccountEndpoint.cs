using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class UnsharePersonalAccountEndpoint : EndpointWithoutRequest<PersonalAccountResponse>
{
    private readonly IPersonalAccountService _personalAccountService;

    public UnsharePersonalAccountEndpoint(IPersonalAccountService personalAccountService)
    {
        _personalAccountService = personalAccountService;
    }

    public override void Configure()
    {
        Post("/personal-finance/accounts/{accountId:guid}/unshare");
        Policies("UserPolicy");
        Summary(s => s.Summary = "Unshare personal account from household");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var accountId = Route<Guid>("accountId");

        try
        {
            var response = await _personalAccountService.UnshareAccountAsync(accountId, ct);
            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}
