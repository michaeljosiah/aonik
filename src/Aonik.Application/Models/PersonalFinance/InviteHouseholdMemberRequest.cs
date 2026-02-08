namespace Aonik.Application.Models.PersonalFinance;

public record InviteHouseholdMemberRequest(
    Guid HouseholdId,
    Guid UserId,
    string Role,
    IReadOnlyList<string>? Permissions);
