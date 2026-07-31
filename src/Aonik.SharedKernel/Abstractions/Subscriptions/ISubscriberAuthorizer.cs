namespace Aonik.SharedKernel.Abstractions.Subscriptions;

/// <summary>
/// Establishes that a subscriber exists and that the current caller may act for it
/// (Spec 087 §11). Module-contributed via <c>IEnumerable&lt;T&gt;</c> DI, keyed by
/// <see cref="SubscriberKinds"/>, matching the pattern used for seeding and provisioning
/// contributors.
///
/// This is the security boundary of a one-tenant-many-subscribers product. The Subscriptions
/// module deliberately stores <c>SubscriberId</c> opaquely and never dereferences it — but tenant
/// scoping alone does not authorise a caller to act for a particular family or party. Without this
/// check, any endpoint passing a caller-controlled id could subscribe, cancel, inspect or consume
/// another subscriber's entitlements inside the same tenant.
///
/// A kind with no registered authorizer <b>fails closed</b>; two authorizers claiming one kind is a
/// startup failure, not last-writer-wins.
/// </summary>
public interface ISubscriberAuthorizer
{
    /// <summary>The subscriber kinds this authorizer owns, from <see cref="SubscriberKinds"/>.</summary>
    IReadOnlyCollection<string> SubscriberKinds { get; }

    /// <summary>
    /// True when the subscriber exists in the current tenant <b>and</b> the current caller may act
    /// for it. Both halves matter: a non-existent id and an unauthorised one are equally refusable,
    /// and distinguishing them to the caller would leak existence.
    /// </summary>
    Task<bool> CanActForAsync(
        SubscriberRef subscriber,
        CancellationToken cancellationToken = default);
}
