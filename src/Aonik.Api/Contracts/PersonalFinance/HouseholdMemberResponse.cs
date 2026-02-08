namespace Aonik.Api.Contracts.PersonalFinance;

public record HouseholdMemberResponse(
    Guid MemberId,
    Guid HouseholdId,
    Guid UserId,
    string Role,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt);
