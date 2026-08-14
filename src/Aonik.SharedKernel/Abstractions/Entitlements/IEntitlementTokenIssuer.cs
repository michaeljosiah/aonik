using Aonik.SharedKernel.Abstractions.Subscriptions;

namespace Aonik.SharedKernel.Abstractions.Entitlements;

/// <summary>
/// Projects the live entitlement read into a signed token (Spec 090 §7).
///
/// <para>
/// <c>IEntitlementReader</c> is unchanged: this spec projects it, it does not replace it, and every server-side
/// caller keeps reading the database. The token exists for the machine that cannot ask — the desktop app on a
/// train, the laptop in a studio with no network — where the alternative is either trusting the client's memory
/// of what it bought or refusing to work offline at all.
/// </para>
/// </summary>
public interface IEntitlementTokenIssuer
{
    /// <summary>
    /// Issue a fresh token for a subscriber, optionally bound to a device fingerprint.
    /// </summary>
    /// <exception cref="InvalidOperationException">No signing key is available. Rotate first; issuing never
    /// generates a key implicitly.</exception>
    Task<IssuedEntitlementToken> IssueAsync(
        SubscriberRef subscriber,
        string? deviceFingerprint = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke by token id or for the whole subscriber.
    ///
    /// <para>
    /// Subscriber-wide revocation is published as the <em>handle</em>, never the subscriber id, so the public
    /// list does not name who was revoked (§9.1).
    /// </para>
    /// </summary>
    Task<bool> RevokeAsync(
        Guid? jti,
        SubscriberRef? subscriber,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The current revocation list: <c>jti</c> values and handles, nothing that names a subscriber.
    /// </summary>
    Task<EntitlementRevocationList> GetRevocationsAsync(CancellationToken cancellationToken = default);
}

/// <param name="Token">The two-part wire string of §5.1.</param>
public sealed record IssuedEntitlementToken(
    string Token,
    Guid Jti,
    string Kid,
    DateTime ExpiresAt,
    DateTime GraceUntil);

/// <param name="Handles">Per-subscriber revocation handles. Random, so the list names nobody.</param>
public sealed record EntitlementRevocationList(
    IReadOnlyList<Guid> TokenIds,
    IReadOnlyList<string> Handles);
