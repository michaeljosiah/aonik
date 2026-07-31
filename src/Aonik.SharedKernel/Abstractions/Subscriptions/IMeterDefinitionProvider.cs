namespace Aonik.SharedKernel.Abstractions.Subscriptions;

/// <summary>
/// Supplies a module's default meters at provisioning (Spec 087 §14.1). Module-contributed via
/// <c>IEnumerable&lt;T&gt;</c> DI.
///
/// This is a <b>seeding</b> contributor, not the validator. The tenant-scoped <c>Meter</c> table is
/// the authority on which codes are valid and on each meter's kind and unit; rows reach it from
/// here and from business-type config packs (ADR-014), and neither source is privileged. Making
/// code registrations authoritative would mean a pack-delivered meter had no owning module and
/// failed validation — which would contradict the platform's own promise that a pricing table is
/// expressible as data with no code change.
/// </summary>
public interface IMeterDefinitionProvider
{
    /// <summary>The meters this module owns. Applied idempotently; an existing row is never overwritten.</summary>
    IReadOnlyCollection<MeterDefinition> GetMeters();
}
