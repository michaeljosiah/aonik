namespace Aonik.Finance.Contracts.Models.PersonalFinance;

public record CreateHouseholdRequest(string Name);

public record InviteHouseholdMemberRequest(
    Guid HouseholdId,
    Guid UserId,
    string Role,
    IReadOnlyList<string>? Permissions);

public record HouseholdResponse(
    Guid HouseholdId,
    string Name,
    HouseholdMemberResponse Owner,
    DateTime CreatedAt);

public record HouseholdMemberResponse(
    Guid MemberId,
    Guid HouseholdId,
    Guid UserId,
    string Role,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt);
