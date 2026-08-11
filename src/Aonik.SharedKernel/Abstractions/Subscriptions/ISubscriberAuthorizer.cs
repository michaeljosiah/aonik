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
    /// <summary>
    /// The subscriber kinds this authorizer owns, from <see cref="Subscriptions.SubscriberKinds"/>.
    /// </summary>
    /// <remarks>
    /// Named <c>SupportedKinds</c> rather than <c>SubscriberKinds</c> deliberately. A property
    /// sharing its simple name with the constants class shadows that class inside every
    /// implementer, so the obvious initializer —
    /// <c>public IReadOnlyCollection&lt;string&gt; SubscriberKinds { get; } = [SubscriberKinds.Group];</c>
    /// — fails with CS0236: the unqualified name binds to the property being declared, not the
    /// type. <c>IShareResourceResolver</c> (Spec 086 §6) avoids the same hazard by pairing
    /// <c>ResourceKinds</c> with <c>ShareResourceKinds</c>.
    /// </remarks>
    IReadOnlyCollection<string> SupportedKinds { get; }

    /// <summary>
    /// True when the subscriber exists in the current tenant <b>and</b> the current caller may act
    /// for it. Both halves matter: a non-existent id and an unauthorised one are equally refusable,
    /// and distinguishing them to the caller would leak existence.
    /// </summary>
    Task<bool> CanActForAsync(
        SubscriberRef subscriber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the caller may change what the subscriber <b>pays for</b> — subscribe, change plan,
    /// cancel, set the payment mandate, buy a top-up.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="CanActForAsync"/> because the two questions are genuinely different
    /// and answering them together gets one of them wrong. Reading entitlements and drawing on them
    /// is what ordinary membership is for — a child in a family consuming their own allowance is the
    /// point of the model. Cancelling the plan or replacing the card is not: without this split, any
    /// accepted member of a family, or any ordinary user in a B2B tenant, could cancel the shared
    /// paid plan and change its payment instrument.
    /// </remarks>
    Task<bool> CanManageBillingForAsync(SubscriberRef subscriber, CancellationToken cancellationToken = default);
}
