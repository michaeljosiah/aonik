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

    /// <summary>
    /// One billing period of a subscription (Spec 087 §12). One line: the plan version at its
    /// pinned price. Settlement credits subscription revenue rather than 4000.
    /// </summary>
    public const string SubscriptionRenewal = "SubscriptionRenewal";

    /// <summary>
    /// Units of a named meter bought outright (Spec 087 §12). Named for the ENTITLEMENT, not for
    /// one product's currency: "one more animated video" and "500 credits" are the same
    /// transaction against different meters, so a type named "CreditPurchase" would have needed a
    /// second type the moment a second product priced differently.
    /// </summary>
    public const string EntitlementPurchase = "EntitlementPurchase";

    /// <summary>A purchase order to a supplier for raw materials (Spec 053 §10) — the inverse
    /// direction of money to <see cref="ProductPurchase"/>: we pay, the supplier is the payee.
    /// Lines reuse the retail shape with <c>ProductId</c> soft-referencing an ingredient.</summary>
    public const string PurchaseOrder = "PurchaseOrder";
}
