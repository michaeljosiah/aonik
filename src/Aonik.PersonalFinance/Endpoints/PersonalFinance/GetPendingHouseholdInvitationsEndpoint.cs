using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class GetPendingHouseholdInvitationsEndpoint : EndpointWithoutRequest<IReadOnlyList<HouseholdInvitationResponse>>
{
    private readonly IHouseholdService _householdService;

    public GetPendingHouseholdInvitationsEndpoint(IHouseholdService householdService)
    {
        _householdService = householdService;
    }

    public override void Configure()
    {
        Get("/personal-finance/households/invitations");
        Policies("UserPolicy");
        Summary(s => s.Summary = "Get pending household invitations");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = await _householdService.GetPendingInvitationsAsync(ct);
        await Send.OkAsync(response, ct);
    }
}
