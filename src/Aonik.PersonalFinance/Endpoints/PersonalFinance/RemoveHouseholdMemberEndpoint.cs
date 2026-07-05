using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class RemoveHouseholdMemberEndpoint : EndpointWithoutRequest
{
    private readonly IHouseholdService _householdService;

    public RemoveHouseholdMemberEndpoint(IHouseholdService householdService)
    {
        _householdService = householdService;
    }

    public override void Configure()
    {
        Delete("/personal-finance/households/{householdId:guid}/members/{userId:guid}");
        Policies("UserPolicy");
        Summary(s => s.Summary = "Remove household member");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var householdId = Route<Guid>("householdId");
        var userId = Route<Guid>("userId");

        try
        {
            await _householdService.RemoveMemberAsync(householdId, userId, ct);
            await Send.NoContentAsync(ct);
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
