namespace Aonik.Finance.Contracts.Services.Payments;

/// <summary>
/// Customer-facing recipient surface. A "recipient" is who a customer sends money to — modelled as a
/// Platform Party reached by a directed <c>Recipient</c> relationship edge, marked <c>Beneficiary</c>
/// for its owning customer, with one or more saved payout rails (<c>ExternalPayoutAccount</c>). This
/// service is a façade: it composes the cross-module party seam (<see cref="Aonik.SharedKernel.Abstractions.IPartyService"/>),
/// the shipped <see cref="IPayoutBeneficiaryService"/> (which owns the party + edge + role + rail
/// stitching), and the party-photo seam — it stores no recipient table of its own.
///
/// Every operation is scoped to a customer: a recipient party id alone never reads or mutates another
/// customer's recipient.
/// </summary>
public interface IRecipientService
{
    /// <summary>
    /// Creates/saves a recipient and a payout rail for the customer. Delegates the party-graph + rail
    /// write to <see cref="IPayoutBeneficiaryService.SaveBeneficiaryAsync"/> (idempotent on the
    /// edge/role), then returns the unified recipient projection.
    /// </summary>
    Task<RecipientResponse> CreateAsync(SavePayoutBeneficiaryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads one recipient (identity + relationship + photo + saved rails) owned by the customer, or null.</summary>
    Task<RecipientResponse?> GetAsync(Guid customerPartyId, Guid recipientPartyId, CancellationToken cancellationToken = default);

    /// <summary>Lists the customer's recipients with name search and paging.</summary>
    Task<RecipientListResponse> ListAsync(Guid customerPartyId, RecipientQuery query, CancellationToken cancellationToken = default);

    /// <summary>Updates the customer→recipient edge (relationship type and/or notes) and returns the refreshed projection.</summary>
    Task<RecipientResponse> UpdateAsync(Guid customerPartyId, Guid recipientPartyId, UpdateRecipientRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-removes a recipient for the customer: soft-deletes their saved rails and deactivates the
    /// owning <c>Recipient</c> edge. The party and all historical orders are left untouched.
    /// </summary>
    Task RemoveAsync(Guid customerPartyId, Guid recipientPartyId, CancellationToken cancellationToken = default);

    /// <summary>Uploads the recipient's photo (original + thumbnails) onto their party profile and returns the URLs.</summary>
    Task<RecipientPhotoResponse> UploadPhotoAsync(
        Guid customerPartyId,
        Guid recipientPartyId,
        string contentType,
        Stream photo,
        CancellationToken cancellationToken = default);
}

/// <summary>A recipient: the payable party, its ownership edge, its photo, and its saved rails.</summary>
public record RecipientResponse(
    Guid RecipientPartyId,
    string DisplayName,
    string RelationshipTypeCode,
    string? PhotoUrl,
    string? PhotoUrlSmall,
    bool IsActive,
    IReadOnlyList<RecipientRailResponse> Rails);

/// <summary>A saved payout destination for a recipient. Never carries the raw account number / MSISDN / wallet id.</summary>
public record RecipientRailResponse(
    Guid ExternalPayoutAccountId,
    string DestinationType,
    string AccountName,
    string MaskedAccountIdentifier,
    string Currency,
    string? BankCode,
    string? MobileNetwork,
    bool IsVerified);

/// <summary>List query: optional name search plus paging (clamped server-side).</summary>
public record RecipientQuery(string? Search = null, int Page = 1, int PageSize = 20);

/// <summary>A page of a customer's recipients.</summary>
public record RecipientListResponse(
    Guid CustomerPartyId,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<RecipientResponse> Recipients);

/// <summary>Editable recipient fields scoped to the ownership edge. Null leaves a field unchanged.</summary>
public record UpdateRecipientRequest(
    string? RelationshipTypeCode = null,
    string? Notes = null);

/// <summary>Recipient photo URLs after an upload.</summary>
public record RecipientPhotoResponse(
    Guid RecipientPartyId,
    string? PhotoUrl,
    string? PhotoUrlMedium,
    string? PhotoUrlSmall,
    string? PhotoUrlTiny);
