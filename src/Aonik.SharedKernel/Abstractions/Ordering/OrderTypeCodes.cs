namespace Aonik.SharedKernel.Abstractions.Ordering;

/// <summary>
/// Order type code constants (Spec 041 / ADR-011). <c>Order.OrderType</c> is an open string —
/// the <c>Aonik.Finance.Entities.Orders.OrderType</c> enum is only a known-values helper — so new
/// types are additive. These constants let cross-module consumers and the core ordering contract
/// name an order type without depending on the Finance enum. The Finance enum remains the
/// source of truth; keep these in lockstep with it.
/// </summary>
public static class OrderTypeCodes
{
    public const string BillPayment = "BillPayment";
    public const string BankTransfer = "BankTransfer";
    public const string CashCollection = "CashCollection";
    public const string Payout = "Payout";
    public const string Collection = "Collection";
    public const string AirtimeTopup = "AirtimeTopup";
    public const string Remittance = "Remittance";

    /// <summary>A purchase of goods. The retail line shape lives on <c>OrderItem</c>.</summary>
    public const string ProductPurchase = "ProductPurchase";

    /// <summary>A purchase order to a supplier for raw materials (Spec 053 §10) — the inverse
    /// direction of money to <see cref="ProductPurchase"/>: we pay, the supplier is the payee.
    /// Lines reuse the retail shape with <c>ProductId</c> soft-referencing an ingredient.</summary>
    public const string PurchaseOrder = "PurchaseOrder";
}
