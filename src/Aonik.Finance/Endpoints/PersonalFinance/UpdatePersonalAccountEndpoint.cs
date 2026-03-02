using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class UpdatePersonalAccountEndpoint : Endpoint<UpdatePersonalAccountRequest, PersonalAccountResponse>
{
    private readonly IPersonalAccountService _personalAccountService;

    public UpdatePersonalAccountEndpoint(IPersonalAccountService personalAccountService)
    {
        _personalAccountService = personalAccountService;
    }

    public override void Configure()
    {
        Patch("/personal-finance/accounts/{id}");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(UpdatePersonalAccountRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var response = await _personalAccountService.UpdateAccountAsync(id, req, ct);
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
