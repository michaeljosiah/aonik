using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetMyHouseholdEndpoint : EndpointWithoutRequest<HouseholdDetailResponse>
{
    private readonly IHouseholdService _householdService;

    public GetMyHouseholdEndpoint(IHouseholdService householdService)
    {
        _householdService = householdService;
    }

    public override void Configure()
    {
        Get("/personal-finance/households/me");
        Policies("UserPolicy");
        Summary(s => s.Summary = "Get my household");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _householdService.GetMyHouseholdAsync(ct);
        if (response == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
