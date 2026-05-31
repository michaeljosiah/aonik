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
    Remittance
}
