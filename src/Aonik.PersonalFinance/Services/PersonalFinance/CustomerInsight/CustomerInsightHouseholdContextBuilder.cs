using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance.CustomerInsight;

/// <summary>
/// Builds the optional <see cref="CustomerInsightHouseholdContext"/> section by
/// projecting the household and its accepted member list, flagging which member
/// is the user the snapshot is being generated for.
/// </summary>
internal static class CustomerInsightHouseholdContextBuilder
{
    public static CustomerInsightHouseholdContext Build(
        Household household,
        IReadOnlyList<HouseholdMember> members,
        Guid currentUserId)
    {
        var memberSummaries = members
            .OrderBy(x => x.UserId)
            .Select(x => new CustomerInsightHouseholdMemberSummary(
                x.UserId,
                string.IsNullOrWhiteSpace(x.Role) ? "member" : x.Role.Trim(),
                x.UserId == currentUserId))
            .ToList();

        return new CustomerInsightHouseholdContext(
            household.Id,
            string.IsNullOrWhiteSpace(household.Name) ? "Household" : household.Name.Trim(),
            members.Count,
            memberSummaries);
    }
}
