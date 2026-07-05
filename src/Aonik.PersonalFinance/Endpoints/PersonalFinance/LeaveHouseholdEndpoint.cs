using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class LeaveHouseholdEndpoint : EndpointWithoutRequest
{
    private readonly IHouseholdService _householdService;

    public LeaveHouseholdEndpoint(IHouseholdService householdService)
    {
        _householdService = householdService;
    }

    public override void Configure()
    {
        Post("/personal-finance/households/{householdId:guid}/leave");
        Policies("UserPolicy");
        Summary(s => s.Summary = "Leave household");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var householdId = Route<Guid>("householdId");

        try
        {
            await _householdService.LeaveHouseholdAsync(householdId, ct);
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
