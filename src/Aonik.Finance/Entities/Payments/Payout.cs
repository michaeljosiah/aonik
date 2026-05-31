using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

public class Payout : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;

    /// <summary>FK to <see cref="ExternalPayoutAccount"/> - the structured payout destination.</summary>
    public Guid? DestinationExternalAccountId { get; set; }

    public Guid? PartnerId { get; set; }
    public Guid ConnectorId { get; set; }

    /// <summary>Idempotent client reference (our tx_ref / clientRef).</summary>
    public string ClientReference { get; set; } = string.Empty;

    /// <summary>Provider-assigned reference (flw_ref / paymentRef), set once the partner responds.</summary>
    public string ProviderReference { get; set; } = string.Empty;

    public string DebitCurrency { get; set; } = string.Empty;
    public decimal? FxRate { get; set; }
    public decimal? ConvertedAmount { get; set; }
    public decimal? Fee { get; set; }
    public string? FeeCurrency { get; set; }
    public string? Narration { get; set; }

    /// <summary>Bank | MobileMoney | Wallet - the rail, mirroring the destination's type.</summary>
    public string DestinationType { get; set; } = string.Empty;

    /// <summary>Order item this payout fulfils, when raised to fulfil an order.</summary>
    public Guid? OrderItemId { get; set; }

    public string? FailureReason { get; set; }

    /// <summary>Redacted vendor response - codes and status only, never PANs / MSISDNs / secrets.</summary>
    public string? RawResponseJson { get; set; }

    /// <summary>PartnerTransactionStatus vocabulary, stored as string.</summary>
    public string Status { get; set; } = string.Empty;
}
