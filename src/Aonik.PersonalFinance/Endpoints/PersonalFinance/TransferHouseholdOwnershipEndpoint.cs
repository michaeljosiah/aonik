using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using FastEndpoints;

namespace Aonik.PersonalFinance.Endpoints;

internal sealed class TransferHouseholdOwnershipEndpoint : Endpoint<TransferOwnershipRequest, HouseholdDetailResponse>
{
    private readonly IHouseholdService _householdService;

    public TransferHouseholdOwnershipEndpoint(IHouseholdService householdService)
    {
        _householdService = householdService;
    }

    public override void Configure()
    {
        Post("/personal-finance/households/{householdId:guid}/transfer-ownership");
        Policies("UserPolicy");
        Summary(s => s.Summary = "Transfer household ownership");
    }

    public override async Task HandleAsync(TransferOwnershipRequest req, CancellationToken ct)
    {
        var householdId = Route<Guid>("householdId");

        try
        {
            var response = await _householdService.TransferOwnershipAsync(householdId, req.NewOwnerUserId, ct);
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
