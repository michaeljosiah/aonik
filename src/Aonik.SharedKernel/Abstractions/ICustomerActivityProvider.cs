namespace Aonik.SharedKernel.Abstractions;

/// <summary>
/// Cross-module contract for retrieving recent finance-side activity for a
/// specific customer (party). Implemented by the Finance module so the
/// Platform module can compose a unified activity feed (audit log entries
/// + documents + finance events) without taking a project reference on
/// Finance entities or DbContext.
/// </summary>
public interface ICustomerActivityProvider
{
    /// <summary>
    /// Returns up to <paramref name="take"/> recent finance-side events for
    /// the given party — order state changes, captured payments, etc. The
    /// caller is expected to merge this list with non-finance sources and
    /// re-sort before returning to the UI.
    /// </summary>
    Task<IReadOnlyList<CustomerActivityEntry>> GetRecentActivityAsync(
        Guid tenantId,
        Guid partyId,
        int take = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single entry in a customer's recent activity feed. Generic enough to
/// represent an order, payment, document upload, or audit log entry.
/// </summary>
/// <param name="Timestamp">When the event happened (UTC).</param>
/// <param name="Kind">Stable kind discriminator (e.g. "order_created",
/// "payment_captured", "document_uploaded", "audit_log"). UI maps this to
/// an icon + tone.</param>
/// <param name="Title">One-line headline shown to the user.</param>
/// <param name="Subtitle">Optional secondary line — typically an amount,
/// reference, or short description.</param>
/// <param name="LinkPath">Optional client-side route to drill into the
/// underlying record (e.g. "/orders/{id}"). Null when the entry is purely
/// informational.</param>
public record CustomerActivityEntry(
    DateTime Timestamp,
    string Kind,
    string Title,
    string? Subtitle,
    string? LinkPath
);
