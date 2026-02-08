namespace Aonik.Application.Models.PersonalFinance;

public record HouseholdMemberResponse(
    Guid MemberId,
    Guid HouseholdId,
    Guid UserId,
    string Role,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAt);
