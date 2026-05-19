using System.Text.Json;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;

namespace Aonik.Finance.Services.PersonalFinance;

internal static class HouseholdMembershipRules
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static readonly IReadOnlyList<string> EmptyPermissions = Array.Empty<string>();

    public static string NormalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Role is required.", nameof(role));
        }

        return role.Trim().ToLowerInvariant() switch
        {
            "owner" => HouseholdRoles.Owner,
            "member" => HouseholdRoles.Manager,
            "manager" => HouseholdRoles.Manager,
            "viewer" => HouseholdRoles.Viewer,
            _ => throw new ArgumentException("Role must be Owner, Manager, or Viewer.", nameof(role))
        };
    }

    public static string NormalizeInvitationStatus(string? invitationStatus)
    {
        if (string.IsNullOrWhiteSpace(invitationStatus))
        {
            return HouseholdInvitationStatuses.Accepted;
        }

        return invitationStatus.Trim().ToLowerInvariant() switch
        {
            "pending" => HouseholdInvitationStatuses.Pending,
            "accepted" => HouseholdInvitationStatuses.Accepted,
            "declined" => HouseholdInvitationStatuses.Declined,
            "removed" => HouseholdInvitationStatuses.Removed,
            _ => HouseholdInvitationStatuses.Accepted
        };
    }

    public static bool IsAccepted(HouseholdMember member)
        => string.Equals(NormalizeInvitationStatus(member.InvitationStatus), HouseholdInvitationStatuses.Accepted, StringComparison.OrdinalIgnoreCase);

    public static bool IsPending(HouseholdMember member)
        => string.Equals(NormalizeInvitationStatus(member.InvitationStatus), HouseholdInvitationStatuses.Pending, StringComparison.OrdinalIgnoreCase);

    public static bool CanManageMembers(HouseholdMember member)
    {
        if (!IsAccepted(member))
        {
            return false;
        }

        var role = NormalizeRole(member.Role);
        return string.Equals(role, HouseholdRoles.Owner, StringComparison.Ordinal)
            || string.Equals(role, HouseholdRoles.Manager, StringComparison.Ordinal);
    }

    public static bool IsOwner(HouseholdMember member)
        => IsAccepted(member) && string.Equals(NormalizeRole(member.Role), HouseholdRoles.Owner, StringComparison.Ordinal);

    public static IReadOnlyList<string> NormalizePermissions(IReadOnlyList<string>? permissions)
    {
        if (permissions == null || permissions.Count == 0)
        {
            return EmptyPermissions;
        }

        return permissions
            .Where(permission => !string.IsNullOrWhiteSpace(permission))
            .Select(permission => permission.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> ParsePermissions(string? permissionsJson)
    {
        if (string.IsNullOrWhiteSpace(permissionsJson))
        {
            return EmptyPermissions;
        }

        try
        {
            var permissions = JsonSerializer.Deserialize<List<string>>(permissionsJson, JsonOptions);
            return NormalizePermissions(permissions);
        }
        catch (JsonException)
        {
            return EmptyPermissions;
        }
    }

    public static string SerializePermissions(IReadOnlyList<string>? permissions)
        => JsonSerializer.Serialize(NormalizePermissions(permissions), JsonOptions);

    public static void NormalizeLegacyMember(HouseholdMember member)
    {
        member.Role = NormalizeRole(member.Role);
        member.InvitationStatus = NormalizeInvitationStatus(member.InvitationStatus);

        if (IsAccepted(member))
        {
            member.InvitedAt ??= member.CreatedAt;
        }
    }
}
