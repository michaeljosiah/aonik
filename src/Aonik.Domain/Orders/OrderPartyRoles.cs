namespace Aonik.Domain.Orders;

/// <summary>
/// Defines order party roles that describe "what this party is doing in this order".
/// These are transaction participation roles used in OrderPartyRole.
/// </summary>
public static class OrderPartyRoles
{
    /// <summary>
    /// The party initiating the order and providing funds
    /// </summary>
    public const string Payer = "Payer";

    /// <summary>
    /// The party originating or sending funds/assets
    /// </summary>
    public const string Sender = "Sender";

    /// <summary>
    /// The party receiving funds/assets or benefits
    /// </summary>
    public const string Receiver = "Receiver";

    /// <summary>
    /// The party to whom payment is being made
    /// </summary>
    public const string Payee = "Payee";
}
