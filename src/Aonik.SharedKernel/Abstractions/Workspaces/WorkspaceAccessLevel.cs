namespace Aonik.SharedKernel.Abstractions.Workspaces;

/// <summary>
/// What a caller may do to a workspace — <strong>platform-owned, closed, and enforced</strong> (Spec 089 §8.1).
///
/// <para>
/// An earlier draft put this in <c>TermsJson</c>, reasoning that a platform enforcing an "editor" role would be
/// interpreting terms, which Spec 086 §6.1 forbids. <strong>That is a bypassable authorisation boundary.</strong>
/// The commit endpoint is a platform HTTP endpoint: a recipient holding a read-only grant needs only to call it
/// directly — with curl, or a modified client, which for an MIT-licensed product is a five-minute exercise. The
/// product-side check would be decoration with nothing behind it, and the grant reader's answer to "is there a
/// grant?" for a read-only recipient is <em>yes</em>.
/// </para>
///
/// <para>
/// The seam is not weakened, it is drawn in the right place. <c>TermsJson</c> stays opaque and the product
/// remains its only reader; the platform learns only <strong>whether this caller may write to this
/// container</strong>, which is the access-control question every endpoint owner must answer for itself.
/// Spec 086 §6.1 forbids interpreting <em>terms</em>. It never asked the platform to leave its own endpoints
/// unguarded.
/// </para>
///
/// <para>
/// Three values, no extension point, no DSL. The ordering is meaningful and comparisons rely on it.
/// </para>
/// </summary>
public enum WorkspaceAccessLevel
{
    /// <summary>
    /// No grant, no ownership. The zero value deliberately: an unset access level must not read as permission.
    /// </summary>
    None = 0,

    /// <summary>Read the manifest and the blobs it names.</summary>
    Read = 1,

    /// <summary>Upload blobs and commit revisions.</summary>
    Write = 2,

    /// <summary>Delete, transfer, and share. Only the owning party.</summary>
    Owner = 3,
}

public static class WorkspaceAccess
{
    /// <summary>
    /// The rule, in one line and with no product involvement (§8.1):
    /// <code>
    /// CommitAsync   requires >= Write
    /// UploadBlob    requires >= Write   (no quota consumption by readers)
    /// GetManifest   requires >= Read
    /// Delete/Share  requires == Owner
    /// </code>
    /// </summary>
    public static bool Allows(this WorkspaceAccessLevel effective, WorkspaceAccessLevel required)
        => effective >= required;
}
