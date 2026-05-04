namespace Aonik.Platform.Services.Identity;

/// <summary>
/// Marker constants for the "pending tenant user" pattern. A pending
/// user is a placeholder <c>User</c> row that exists in a tenant
/// BEFORE the human has logged in via the IdP — it captures the email
/// (and optional display name) so the first IdP login can be matched
/// by email and "linked" rather than creating a brand-new user.
/// <para>
/// Two kinds of pending users:
/// </para>
/// <list type="bullet">
///   <item><description>
/// <b>Owner</b> — created at tenant bootstrap or admin-driven tenant
/// creation. The email belongs to the customer's first admin who will
/// receive the <c>TenantAdmin</c> role on first login.
///   </description></item>
///   <item><description>
/// <b>Invite</b> — created when a tenant admin invites a user via the
/// access management UI. Roles are pre-assigned to the placeholder so
/// the user lands with the right permissions on first login.
///   </description></item>
/// </list>
/// Both kinds share the same <see cref="PendingOwnerIssuer"/> value so
/// the JIT lookup in <c>UserIdentityService</c> only has to filter by
/// one issuer; the subject prefix (<c>owner:</c> vs <c>invite:</c>)
/// distinguishes them when needed.
/// </summary>
internal static class BootstrapIdentityConstants
{
    /// <summary>
    /// Issuer marker used on pending placeholder users (both owners
    /// and invites). Real IdP issuers (Auth0 domain, Entra issuer URL)
    /// never collide with this value.
    /// </summary>
    public const string PendingOwnerIssuer = "aonik-bootstrap";

    /// <summary>
    /// Subject for an owner placeholder. Format: <c>owner:{email_lower}</c>.
    /// </summary>
    public static string CreatePendingOwnerSubject(string email)
        => $"owner:{email.Trim().ToLowerInvariant()}";

    /// <summary>
    /// Subject for an invited-user placeholder. Format:
    /// <c>invite:{email_lower}</c>. Distinguishes invitees from owners
    /// in audit logs even though both live under the same issuer.
    /// </summary>
    public static string CreatePendingInviteSubject(string email)
        => $"invite:{email.Trim().ToLowerInvariant()}";

    /// <summary>
    /// True when the issuer is the placeholder marker — i.e. this row
    /// is awaiting first IdP login. Used by the JIT linker so it can
    /// match either an owner or an invite by email.
    /// </summary>
    public static bool IsPendingPlaceholderIssuer(string? issuer)
        => string.Equals(issuer, PendingOwnerIssuer, StringComparison.Ordinal);
}
