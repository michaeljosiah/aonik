using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface IHouseholdService
{
    Task<HouseholdResponse> CreateHouseholdAsync(
        CreateHouseholdRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdInvitationResponse> InviteMemberAsync(
        InviteHouseholdMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<HouseholdMemberResponse> AcceptInvitationAsync(
        Guid householdId,
        CancellationToken cancellationToken = default);

    Task DeclineInvitationAsync(
        Guid householdId,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task LeaveHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken = default);

    Task<HouseholdDetailResponse> TransferOwnershipAsync(
        Guid householdId,
        Guid newOwnerUserId,
        CancellationToken cancellationToken = default);

    Task<HouseholdDetailResponse?> GetMyHouseholdAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HouseholdInvitationResponse>> GetPendingInvitationsAsync(CancellationToken cancellationToken = default);
}
