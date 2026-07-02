namespace Aonik.Commerce.Contracts.Api.Production;

/// <summary>HTTP request bodies for the production-order admin endpoints (Spec 056). Mapped to
/// service commands.</summary>
public record CreateProductionOrderRequest(
    DateTime PlannedFor,
    IReadOnlyList<CreateProductionOrderLine> Lines,
    string? Notes);

/// <summary>One (variant, portions) line of a create-production-order request.</summary>
public record CreateProductionOrderLine(Guid ProductVariantId, decimal PlannedQuantity);

/// <summary>Seeds a production order from the Spec 055 production sheet for a UTC window
/// (half-open [FromUtc, ToUtc)); PlannedFor defaults to FromUtc.</summary>
public record CreateProductionOrderFromSheetRequest(
    DateTime FromUtc,
    DateTime ToUtc,
    DateTime? PlannedFor,
    string? Notes);

/// <summary>Completes a production run; omitted actuals default each line's produced quantity to
/// its planned quantity. YieldFinishedGoods (default true) increments each produced variant's
/// on-hand — turn off for make-to-order runs.</summary>
public record CompleteProductionOrderRequest(
    IReadOnlyList<CompleteProductionOrderLine>? ActualQuantities,
    bool YieldFinishedGoods = true);

/// <summary>One (line, produced portions) entry of a complete request; 0 records a failed batch.</summary>
public record CompleteProductionOrderLine(Guid ProductionOrderLineId, decimal ProducedQuantity);

public record CancelProductionOrderRequest(string? Reason);
