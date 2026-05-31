using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Contracts.Services.Payments;

/// <summary>
/// Saves and lists a customer's payout beneficiaries. A "beneficiary" is the pairing of a payable
/// party (the recipient) with one or more saved payout destinations (<c>ExternalPayoutAccount</c>),
/// owned by a customer through a directed <c>Recipient</c> party-relationship edge.
///
/// Saving stitches three things together via the cross-module party seam (<see cref="IPartyService"/>):
/// the recipient party, the customer→recipient relationship edge, and the recipient's
/// <c>Beneficiary</c> role — then persists the structured payout destination in Finance. The whole
/// operation is idempotent on the edge/role so re-saving another rail for the same recipient does
/// not duplicate the relationship.
/// </summary>
public interface IPayoutBeneficiaryService
{
    /// <summary>
    /// Persists a payout destination for a customer and ensures the customer→recipient relationship
    /// and the recipient's Beneficiary role exist. Creates the recipient party when
    /// <see cref="SavePayoutBeneficiaryRequest.BeneficiaryPartyId"/> is not supplied.
    /// </summary>
    Task<PayoutBeneficiaryResponse> SaveBeneficiaryAsync(
        SavePayoutBeneficiaryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every saved payout destination owned by the customer — the recipients the customer has
    /// a relationship edge to that also have a stored <c>ExternalPayoutAccount</c>.
    /// </summary>
    Task<IReadOnlyList<PayoutBeneficiaryResponse>> ListBeneficiariesAsync(
        Guid customerPartyId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to save a payout beneficiary. Carries the recipient identity (existing party id, or the
/// display details to create one) plus the structured payout destination. Never carries the raw
/// account number / MSISDN / wallet id — only the <see cref="MaskedAccountIdentifier"/> and the
/// optional reusable <see cref="ProviderBeneficiaryId"/> token (Spec 031 sensitive-data rule).
/// </summary>
public record SavePayoutBeneficiaryRequest(
    Guid CustomerPartyId,
    string DestinationType,
    string AccountName,
    string Currency,
    string MaskedAccountIdentifier,
    string? BankCode = null,
    string? BranchCode = null,
    string? MobileNetwork = null,
    string? ProviderBeneficiaryId = null,
    Guid? PartnerId = null,
    Guid? ConnectorId = null,
    Guid? BeneficiaryPartyId = null,
    string? BeneficiaryDisplayName = null,
    string BeneficiaryPartyType = "Person",
    string RelationshipTypeCode = PartyRelationshipTypeCodes.Recipient,
    string? Notes = null);

/// <summary>
/// A saved payout beneficiary: the recipient party, the rail, and the ownership edge type.
/// </summary>
public record PayoutBeneficiaryResponse(
    Guid ExternalPayoutAccountId,
    Guid CustomerPartyId,
    Guid BeneficiaryPartyId,
    string BeneficiaryName,
    string DestinationType,
    string MaskedAccountIdentifier,
    string Currency,
    string? BankCode,
    string? MobileNetwork,
    string RelationshipTypeCode,
    bool IsVerified);

/// <summary>Envelope for the customer's saved payout beneficiaries.</summary>
public record PayoutBeneficiaryListResponse(
    Guid CustomerPartyId,
    IReadOnlyList<PayoutBeneficiaryResponse> Beneficiaries);
