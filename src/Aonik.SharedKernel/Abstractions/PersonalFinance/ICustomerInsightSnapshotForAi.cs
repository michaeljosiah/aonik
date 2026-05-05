namespace Aonik.SharedKernel.Abstractions.PersonalFinance;

/// <summary>
/// Narrow read surface that <c>Aonik.Ai.CustomerInsightAiSummaryService</c>
/// uses to fetch the snapshot it summarises. Returns a flattened DTO with
/// the fields the AI summariser actually consumes plus the snapshot
/// document pre-serialised as JSON (which is what the LLM prompt receives
/// anyway). Lives on SharedKernel so Ai can read snapshots without taking
/// a back-pointing reference on Finance.
/// </summary>
/// <remarks>
/// Finance owns the implementation as a thin wrapper over its existing
/// <c>ICustomerInsightSnapshotReader</c>, projecting the rich domain
/// response into this AI-shaped envelope.
/// </remarks>
public interface ICustomerInsightSnapshotForAi
{
    /// <summary>
    /// Returns the snapshot identified by <paramref name="snapshotId"/>,
    /// or <c>null</c> when the snapshot is missing or its document has
    /// not been materialised. Tenant scoping is enforced by the
    /// implementation's query filters.
    /// </summary>
    Task<CustomerInsightSnapshotForAi?> GetSnapshotForSummaryAsync(
        Guid snapshotId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Flattened snapshot view for AI summarisation. <see cref="SnapshotJson"/>
/// is the canonical document body (already serialised) that gets passed
/// straight into the LLM prompt — Ai never needs to introspect the full
/// graph of <c>CustomerInsightSnapshotDocument</c> records.
/// </summary>
public sealed record CustomerInsightSnapshotForAi(
    Guid Id,
    Guid TenantId,
    Guid UserId,
    DateTime AsOfUtc,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    int Version,
    string SnapshotJson);
