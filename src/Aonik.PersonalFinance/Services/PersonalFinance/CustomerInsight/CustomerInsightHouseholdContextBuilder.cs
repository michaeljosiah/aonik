using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;

namespace Aonik.PersonalFinance.Services.CustomerInsight;

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
            .Where(HouseholdMembershipRules.IsAcceptedUserMember)
            .OrderBy(x => x.UserId)
            .Select(x => new CustomerInsightHouseholdMemberSummary(
                x.UserId!.Value,
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
