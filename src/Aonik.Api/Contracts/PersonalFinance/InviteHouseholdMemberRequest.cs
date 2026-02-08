namespace Aonik.Api.Contracts.PersonalFinance;

public record InviteHouseholdMemberRequest(
    Guid UserId,
    string Role,
    IReadOnlyList<string>? Permissions);
