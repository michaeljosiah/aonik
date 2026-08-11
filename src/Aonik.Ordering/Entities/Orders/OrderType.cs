namespace Aonik.Finance.Entities.Orders;

public enum OrderType
{
    BillPayment,
    BankTransfer,
    CashCollection,

    // Spec 031 - service-intent only; the rail (bank / mobile-money / wallet / card) lives on the
    // execution entity (Payout.DestinationType, PaymentIntent.CollectionMethod), not here.
    Payout,
    Collection,
    AirtimeTopup,
    Remittance,

    // Spec 041 / ADR-011 - Order is a core "intent to transact" record, not only a financial
    // service. A product purchase is just another order type: the retail line shape lives on
    // OrderItem (Quantity / UnitPrice / ProductId / Sku); funding still flows through a
    // PaymentIntent and billing through an Invoice, exactly as the financial types do.
    ProductPurchase,

    // Spec 053 - a purchase order to a supplier for raw materials: the inverse direction of
    // money to ProductPurchase (we pay; the supplier is the payee). Lines reuse the retail
    // shape with ProductId soft-referencing an ingredient. Kept in lockstep with
    // SharedKernel.Abstractions.Ordering.OrderTypeCodes (Order.OrderType is an open string;
    // this enum is only the known-values helper).
    PurchaseOrder,

    // Spec 087 §12 - subscriptions ride the same order spine. Kept in lockstep with
    // SharedKernel.Abstractions.Ordering.OrderTypeCodes, as the comment above requires.
    SubscriptionRenewal,
    EntitlementPurchase
}
