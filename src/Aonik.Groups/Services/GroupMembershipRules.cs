using System.Text.Json;

using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Abstractions.Groups;

namespace Aonik.Groups.Services;

/// <summary>
/// The generic half of the old <c>HouseholdMembershipRules</c> (Spec 086 §8.1) — role and status
/// normalisation, the permissions blob, and the legacy-row repair every read has to do.
/// </summary>
/// <remarks>
/// Nothing here knows what a household is. The normalisation is load-bearing rather than cosmetic:
/// rows written before Spec 020's status column existed carry a null <c>InvitationStatus</c> and an
/// unnormalised role, and every predicate in the service would answer wrongly on them.
/// <c>PermissionsJson</c> is carried verbatim and deprecated — <c>ShareGrant</c> is what actually
/// enforces visibility (Spec 048).
/// </remarks>
public static class GroupMembershipRules
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static readonly IReadOnlyList<string> EmptyPermissions = Array.Empty<string>();

    public static string NormalizeRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            throw new ArgumentException("Role is required.", nameof(role));
        }

        return role.Trim().ToLowerInvariant() switch
        {
            "owner" => GroupRoles.Owner,
            // "member" is the vocabulary the API and CLI have always used for a manager. Mapping it
            // here rather than renaming the stored value is what keeps the wire unchanged.
            "member" => GroupRoles.Manager,
            "manager" => GroupRoles.Manager,
            "viewer" => GroupRoles.Viewer,
            _ => throw new ArgumentException("Role must be Owner, Manager, or Viewer.", nameof(role))
        };
    }

    /// <summary>
    /// Normalises a stored status, treating an unknown or missing one as accepted.
    /// </summary>
    /// <remarks>
    /// Defaulting to accepted looks permissive, and is deliberate: rows predating the status column
    /// are members who were already in the group, and defaulting them to pending would silently
    /// evict everyone who joined before Spec 020.
    /// </remarks>
    public static string NormalizeStatus(string? invitationStatus)
    {
        if (string.IsNullOrWhiteSpace(invitationStatus))
        {
            return GroupMemberStatuses.Accepted;
        }

        return invitationStatus.Trim().ToLowerInvariant() switch
        {
            "pending" => GroupMemberStatuses.Pending,
            "accepted" => GroupMemberStatuses.Accepted,
            "declined" => GroupMemberStatuses.Declined,
            "removed" => GroupMemberStatuses.Removed,
            _ => GroupMemberStatuses.Accepted
        };
    }

    public static bool IsAccepted(HouseholdMember member)
        => string.Equals(NormalizeStatus(member.InvitationStatus), GroupMemberStatuses.Accepted, StringComparison.OrdinalIgnoreCase);

    public static bool IsPending(HouseholdMember member)
        => string.Equals(NormalizeStatus(member.InvitationStatus), GroupMemberStatuses.Pending, StringComparison.OrdinalIgnoreCase);

    public static bool IsOwner(HouseholdMember member)
        => IsAccepted(member) && string.Equals(NormalizeRole(member.Role), GroupRoles.Owner, StringComparison.Ordinal);

    public static bool CanManageMembers(HouseholdMember member)
    {
        if (!IsAccepted(member))
        {
            return false;
        }

        var role = NormalizeRole(member.Role);
        return string.Equals(role, GroupRoles.Owner, StringComparison.Ordinal)
            || string.Equals(role, GroupRoles.Manager, StringComparison.Ordinal);
    }

    public static bool IsExpired(HouseholdMember member, DateTime now)
        => member.ExpiresAt.HasValue && member.ExpiresAt.Value <= now;

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

    public static void NormalizeLegacy(HouseholdMember member)
    {
        member.Role = NormalizeRole(member.Role);
        member.InvitationStatus = NormalizeStatus(member.InvitationStatus);

        if (IsAccepted(member))
        {
            member.InvitedAt ??= member.CreatedAt;
        }
    }

    public static GroupMemberDto ToDto(HouseholdMember member)
    {
        NormalizeLegacy(member);

        return new GroupMemberDto(
            member.Id,
            member.HouseholdId,
            // Through the transition a member may still have no party — the P3 backfill is what
            // closes that, and it is disabled by default. Guid.Empty rather than a throw: a member
            // the caller cannot act on is better than an endpoint that 500s on legacy data.
            member.PartyId ?? Guid.Empty,
            member.UserId,
            NormalizeRole(member.Role),
            NormalizeStatus(member.InvitationStatus),
            member.InvitedAt,
            member.RespondedAt);
    }
}
