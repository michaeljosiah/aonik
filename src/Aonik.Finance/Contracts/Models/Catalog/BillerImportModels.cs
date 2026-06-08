namespace Aonik.Finance.Contracts.Models.Catalog;

// ── Partner biller catalogue import (Spec 040 §9) ─────────────────────────────
// Request/response contracts for the operator-triggered import: preview the live partner catalogue,
// then import the selected billers. No money moves — this is a Catalog.Write-gated reference-data sync.

/// <summary>
/// A configured partner connector the operator can import from (the wizard's Source step). Only
/// connectors whose type has a registered bill-payment implementation are returned. IsSandbox marks
/// the Simulated fallback.
/// </summary>
public record BillerImportSourceItem(Guid ConnectorId, string ConnectorType, string Status, bool IsSandbox);

public record BillerImportSourcesResponse(List<BillerImportSourceItem> Sources);

/// <summary>Preview the live catalogue exposed by a configured partner connector. Persists nothing.</summary>
public record BillerImportPreviewRequest(Guid ConnectorId, string? CategoryCode = null, string? Country = null);

/// <summary>
/// One previewed biller, annotated against what is already imported for this connector:
/// <see cref="ImportStatus"/> is "New" (no mapping), "Mapped" (mapping exists, unchanged), or
/// "Changed" (mapping exists, name/amount differs). <see cref="ServiceCategory"/> is the connector's
/// classification ("BillPayment" / "AirtimeTopup") as a string.
/// </summary>
public record BillerImportPreviewEntry(
    string BillerCode,
    string BillerName,
    string CategoryCode,
    string CategoryName,
    string ServiceCategory,
    int ServiceCount,
    string ImportStatus,
    string? ChangeNote);

public record BillerImportPreviewResponse(List<BillerImportPreviewEntry> Entries);

/// <summary>
/// The operator's selection. Entries carry identities only (biller code + optional item codes); the
/// service re-reads authoritative field values from the partner at import time rather than trusting
/// client-supplied catalogue data (Spec 040 §8).
/// </summary>
public record BillerImportSelector(string BillerCode, List<string>? ItemCodes = null);

public record BillerImportRequest(Guid ConnectorId, List<BillerImportSelector> Entries);

public record BillerImportSummaryResponse(
    int BillersCreated,
    int BillersUpdated,
    int ServicesCreated,
    int ServicesUpdated,
    int Deactivated);
