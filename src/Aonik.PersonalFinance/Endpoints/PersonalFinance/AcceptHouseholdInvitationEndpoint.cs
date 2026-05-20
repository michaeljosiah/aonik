using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class AcceptHouseholdInvitationEndpoint : EndpointWithoutRequest<HouseholdMemberResponse>
{
    private readonly IHouseholdService _householdService;

    public AcceptHouseholdInvitationEndpoint(IHouseholdService householdService)
    {
        _householdService = householdService;
    }

    public override void Configure()
    {
        Post("/personal-finance/households/{householdId:guid}/members/accept");
        Policies("UserPolicy");
        Summary(s => s.Summary = "Accept household invitation");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var householdId = Route<Guid>("householdId");

        try
        {
            var response = await _householdService.AcceptInvitationAsync(householdId, ct);
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
