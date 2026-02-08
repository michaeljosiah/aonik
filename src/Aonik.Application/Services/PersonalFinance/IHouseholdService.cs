using Aonik.Application.Models.PersonalFinance;

namespace Aonik.Application.Services.PersonalFinance;

public interface IHouseholdService
{
    Task<HouseholdResponse> CreateHouseholdAsync(
        CreateHouseholdRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberResponse> InviteMemberAsync(
        InviteHouseholdMemberRequest request,
        CancellationToken cancellationToken = default);
}
