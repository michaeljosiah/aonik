using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IHouseholdService
{
    Task<HouseholdResponse> CreateHouseholdAsync(
        CreateHouseholdRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberResponse> InviteMemberAsync(
        InviteHouseholdMemberRequest request,
        CancellationToken cancellationToken = default);
}
