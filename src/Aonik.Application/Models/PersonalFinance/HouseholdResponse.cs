namespace Aonik.Application.Models.PersonalFinance;

public record HouseholdResponse(
    Guid HouseholdId,
    string Name,
    HouseholdMemberResponse Owner,
    DateTime CreatedAt);
