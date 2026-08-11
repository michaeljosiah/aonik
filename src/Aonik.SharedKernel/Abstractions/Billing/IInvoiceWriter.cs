namespace Aonik.SharedKernel.Abstractions.Billing;

/// <summary>
/// Write-side contract for raising an invoice for an order (Spec 042 §12) — the write mirror of the
/// ADR-006 read contracts. Implemented by <c>Aonik.Finance</c> and consumed by modules that must
/// bill (e.g. <c>Aonik.Commerce</c> at checkout) without referencing Finance. Keeps invoice
/// mechanics in Finance, where the ledger and customer accounts live.
/// </summary>
public interface IInvoiceWriter
{
    Task<InvoiceRef> CreateForOrderAsync(CreateInvoiceForOrderCommand command, CancellationToken cancellationToken = default);
}

/// <summary>One invoice line.</summary>
public sealed record InvoiceLineSpec(string Description, decimal Quantity, decimal UnitPrice);

/// <summary>Create an invoice that bills <paramref name="CustomerId"/> for the given order.</summary>
public sealed record CreateInvoiceForOrderCommand(
    Guid OrderId,
    Guid CustomerId,
    string Currency,
    IReadOnlyList<InvoiceLineSpec> Lines,
    DateTime? DueUtc = null,
    /// <summary>
    /// Optional idempotency key (Spec 088 §8), matching <c>IOrderService</c>'s behaviour. When
    /// supplied, re-issuing the same key returns the original invoice rather than raising a second
    /// one — which is what stops a renewal job that dies mid-flight from billing twice.
    /// </summary>
    string? IdempotencyKey = null);

/// <summary>A lightweight reference to the created invoice.</summary>
public sealed record InvoiceRef(Guid InvoiceId, string InvoiceNumber, decimal Total, string Currency);
