namespace Aonik.Api.Contracts.PersonalFinance;

public record HouseholdResponse(
    Guid HouseholdId,
    string Name,
    HouseholdMemberResponse Owner,
    DateTime CreatedAt);
