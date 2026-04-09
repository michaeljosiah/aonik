using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class SharePersonalAccountEndpoint : Endpoint<ShareAccountWithHouseholdRequest, PersonalAccountResponse>
{
    private readonly IPersonalAccountService _personalAccountService;

    public SharePersonalAccountEndpoint(IPersonalAccountService personalAccountService)
    {
        _personalAccountService = personalAccountService;
    }

    public override void Configure()
    {
        Post("/personal-finance/accounts/{accountId:guid}/share");
        Policies("UserPolicy");
        Summary(s => s.Summary = "Share personal account with household");
    }

    public override async Task HandleAsync(ShareAccountWithHouseholdRequest req, CancellationToken ct)
    {
        var accountId = Route<Guid>("accountId");

        try
        {
            var response = await _personalAccountService.ShareAccountWithHouseholdAsync(accountId, req, ct);
            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message, 422);
        }
        catch (UnauthorizedAccessException ex)
        {
            ThrowError(ex.Message, 403);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(ex.Message, 409);
        }
    }
}
