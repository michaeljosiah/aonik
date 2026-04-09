using Aonik.Finance.Contracts.Services.PersonalFinance;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.PersonalFinance;

internal sealed class DeclineHouseholdInvitationEndpoint : EndpointWithoutRequest
{
    private readonly IHouseholdService _householdService;

    public DeclineHouseholdInvitationEndpoint(IHouseholdService householdService)
    {
        _householdService = householdService;
    }

    public override void Configure()
    {
        Post("/personal-finance/households/{householdId:guid}/members/decline");
        Policies("UserPolicy");
        Summary(s => s.Summary = "Decline household invitation");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var householdId = Route<Guid>("householdId");

        try
        {
            await _householdService.DeclineInvitationAsync(householdId, ct);
            await Send.NoContentAsync(ct);
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
