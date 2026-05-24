namespace Aonik.Platform.Contracts.Services.Identity;

/// <summary>
/// Result of provisioning a pending tenant user (owner or invite).
/// </summary>
/// <param name="UserId">Id of the newly created or already-existing placeholder user.</param>
/// <param name="PartyId">Id of the linked Party record.</param>
/// <param name="UserPartyId">Id of the User↔Party link.</param>
/// <param name="WasCreated">
/// <c>true</c> if a new placeholder was created; <c>false</c> if a
/// matching pending user already existed and was reused.
/// </param>
public sealed record PendingTenantUserResult(
    Guid UserId,
    Guid PartyId,
    Guid UserPartyId,
    bool WasCreated);

/// <summary>
/// Single source for the "pending tenant user" pattern — creates a
/// placeholder <c>User</c>, a backing <c>Party</c>, the
/// <c>UserParty</c> link, and a <c>PersonProfile</c> with
/// <c>IdvStatus = "Pending"</c>. The placeholder waits for first IdP
/// login, at which point <c>UserIdentityService</c> matches by email
/// and links the real external identity onto this row.
/// <para>
/// Used by:
/// </para>
/// <list type="bullet">
///   <item><description>
/// <c>BootstrapService</c> for the host tenant's first owner.
///   </description></item>
///   <item><description>
/// <c>TenantService.CreateTenantAsync</c> for the initial owner of an
/// admin-created tenant.
///   </description></item>
///   <item><description>
/// <c>AccessManagementService.InviteUserAsync</c> for invited tenant
/// users.
///   </description></item>
/// </list>
/// </summary>
public interface IPendingTenantUserProvisioner
{
    /// <summary>
    /// Provision a pending owner placeholder for <paramref name="tenantId"/>.
    /// Idempotent: if a placeholder already exists for the same email,
    /// the existing one is returned.
    /// </summary>
    Task<PendingTenantUserResult> ProvisionPendingOwnerAsync(
        Guid tenantId,
        string email,
        string? displayName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Provision a pending invite placeholder for <paramref name="tenantId"/>.
    /// Idempotent: if an invite already exists for the same email, the
    /// existing one is returned.
    /// <para>
    /// When <paramref name="existingPartyId"/> is supplied, the
    /// placeholder is linked to that party (which must belong to the
    /// same tenant) instead of provisioning a fresh Individual party.
    /// This is the "invite an existing customer / contact as a user"
    /// path. Re-invoking with the same email but a different party
    /// throws — operators must manage existing users directly.
    /// </para>
    /// </summary>
    Task<PendingTenantUserResult> ProvisionPendingInviteAsync(
        Guid tenantId,
        string email,
        string? displayName,
        Guid? existingPartyId = null,
        CancellationToken cancellationToken = default);
}
