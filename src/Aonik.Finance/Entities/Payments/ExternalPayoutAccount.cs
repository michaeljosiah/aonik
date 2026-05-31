using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

/// <summary>
/// Reusable, verified payout destination - a saved beneficiary plus the name-enquiry result.
/// Gives <see cref="Payout.DestinationExternalAccountId"/> a real referent and a home for the
/// PayoutDestination rails (bank / mobile-money / wallet).
///
/// Links back to the beneficiary through the optional <see cref="BeneficiaryPartyId"/> - a soft
/// Guid reference to the Platform Party (read via PartyReadModel), the same convention
/// Order.PayerPartyId / PaymentIntent.PayeePartyId use; null for a name-enquiry-only destination
/// not yet tied to a known party.
///
/// Stores a MASKED identifier plus the connector's reusable ProviderBeneficiaryId token (and an
/// optional VaultRef) - never the raw account number / MSISDN / wallet id, mirroring the verified
/// PartyAccount MaskedIdentifier + ProviderAccountReference convention.
/// </summary>
public class ExternalPayoutAccount : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Soft Guid reference to the Platform Party that owns this destination; null until tied to a party.</summary>
    public Guid? BeneficiaryPartyId { get; set; }

    public Guid? PartnerId { get; set; }
    public Guid? ConnectorId { get; set; }

    /// <summary>Bank | MobileMoney | Wallet.</summary>
    public string DestinationType { get; set; } = string.Empty;

    public string? BankCode { get; set; }
    public string? BranchCode { get; set; }
    public string? MobileNetwork { get; set; }

    /// <summary>Masked display identifier (e.g. last-four) - never the raw account number / MSISDN / wallet id.</summary>
    public string MaskedAccountIdentifier { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;

    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }

    /// <summary>Connector's reusable beneficiary token, used in place of the raw identifier at execution.</summary>
    public string? ProviderBeneficiaryId { get; set; }

    /// <summary>Optional vault reference behind which the full value is held when execution needs it.</summary>
    public string? VaultRef { get; set; }
}
