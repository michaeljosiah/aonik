using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Services.Payments;

/// <summary>
/// Persists payout beneficiaries and the customer→recipient ownership graph. The party graph (party,
/// relationship edge, Beneficiary role) is written through the cross-module <see cref="IPartyService"/>
/// seam; the structured destination (<see cref="ExternalPayoutAccount"/>) is written through Finance's
/// own context. Each party-seam call persists independently (separate module DbContext), so this
/// service makes them idempotent rather than relying on a shared transaction.
/// </summary>
internal sealed class PayoutBeneficiaryService : IPayoutBeneficiaryService
{
    /// <summary>The recipient's Beneficiary role is scoped to the owning customer.</summary>
    private const string CustomerContextType = "Customer";

    private readonly FinanceDbContext _dbContext;
    private readonly IPartyService _partyService;
    private readonly ITenantProvider _tenantProvider;

    public PayoutBeneficiaryService(
        FinanceDbContext dbContext,
        IPartyService partyService,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _partyService = partyService;
        _tenantProvider = tenantProvider;
    }

    public async Task<PayoutBeneficiaryResponse> SaveBeneficiaryAsync(
        SavePayoutBeneficiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerPartyId == Guid.Empty)
        {
            throw new ArgumentException("Customer party id is required.", nameof(request.CustomerPartyId));
        }

        if (string.IsNullOrWhiteSpace(request.DestinationType))
        {
            throw new ArgumentException("Destination type is required.", nameof(request.DestinationType));
        }

        if (string.IsNullOrWhiteSpace(request.AccountName))
        {
            throw new ArgumentException("Account name is required.", nameof(request.AccountName));
        }

        if (string.IsNullOrWhiteSpace(request.Currency))
        {
            throw new ArgumentException("Currency is required.", nameof(request.Currency));
        }

        if (string.IsNullOrWhiteSpace(request.MaskedAccountIdentifier))
        {
            throw new ArgumentException("Masked account identifier is required.", nameof(request.MaskedAccountIdentifier));
        }

        var relationshipTypeCode = string.IsNullOrWhiteSpace(request.RelationshipTypeCode)
            ? PartyRelationshipTypeCodes.Recipient
            : request.RelationshipTypeCode.Trim();

        // 1) Resolve the recipient party — reuse an existing one or create it.
        var (beneficiaryPartyId, beneficiaryName) =
            await ResolveBeneficiaryPartyAsync(request, cancellationToken);

        // 2) Find-or-create the customer→recipient edge. CreateRelationshipAsync always inserts, so we
        //    dedupe against the customer's existing relationships first (no unique index backs this).
        await EnsureRelationshipAsync(
            request.CustomerPartyId,
            beneficiaryPartyId,
            relationshipTypeCode,
            request.Notes,
            cancellationToken);

        // 3) Mark the recipient payable in the context of this customer (idempotent).
        await _partyService.AssignPartyRoleAsync(
            beneficiaryPartyId,
            PartyRoleCodes.Beneficiary,
            CustomerContextType,
            request.CustomerPartyId,
            cancellationToken);

        // 4) Persist the structured payout destination (masked identifier + token only, never raw PAN/MSISDN).
        var account = new ExternalPayoutAccount
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantProvider.GetCurrentTenantId(),
            CustomerPartyId = request.CustomerPartyId,
            BeneficiaryPartyId = beneficiaryPartyId,
            PartnerId = request.PartnerId,
            ConnectorId = request.ConnectorId,
            DestinationType = request.DestinationType.Trim(),
            BankCode = Normalize(request.BankCode),
            BranchCode = Normalize(request.BranchCode),
            MobileNetwork = Normalize(request.MobileNetwork),
            MaskedAccountIdentifier = request.MaskedAccountIdentifier.Trim(),
            AccountName = request.AccountName.Trim(),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            ProviderBeneficiaryId = Normalize(request.ProviderBeneficiaryId),
            IsVerified = false
        };

        _dbContext.ExternalPayoutAccounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PayoutBeneficiaryResponse(
            account.Id,
            request.CustomerPartyId,
            beneficiaryPartyId,
            beneficiaryName,
            account.DestinationType,
            account.MaskedAccountIdentifier,
            account.Currency,
            account.BankCode,
            account.MobileNetwork,
            relationshipTypeCode,
            account.IsVerified);
    }

    public async Task<IReadOnlyList<PayoutBeneficiaryResponse>> ListBeneficiariesAsync(
        Guid customerPartyId,
        CancellationToken cancellationToken = default)
    {
        if (customerPartyId == Guid.Empty)
        {
            throw new ArgumentException("Customer party id is required.", nameof(customerPartyId));
        }

        var relationships = await _partyService.GetRelationshipsAsync(customerPartyId, cancellationToken);

        // Outgoing edges from this customer identify the recipient parties they own. A party can have
        // more than one edge (e.g. Recipient + a kinship type); keep the first for display purposes.
        var recipientsByPartyId = relationships
            .Where(relationship => relationship.FromPartyId == customerPartyId)
            .GroupBy(relationship => relationship.ToPartyId)
            .ToDictionary(group => group.Key, group => group.First());

        if (recipientsByPartyId.Count == 0)
        {
            return Array.Empty<PayoutBeneficiaryResponse>();
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var recipientPartyIds = recipientsByPartyId.Keys.ToList();

        var accounts = await _dbContext.ExternalPayoutAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId
                              && account.CustomerPartyId == customerPartyId
                              && account.BeneficiaryPartyId != null
                              && recipientPartyIds.Contains(account.BeneficiaryPartyId.Value))
            .OrderByDescending(account => account.CreatedAt)
            .ToListAsync(cancellationToken);

        return accounts
            .Select(account =>
            {
                var recipient = recipientsByPartyId[account.BeneficiaryPartyId!.Value];
                return new PayoutBeneficiaryResponse(
                    account.Id,
                    customerPartyId,
                    account.BeneficiaryPartyId.Value,
                    recipient.ToPartyName,
                    account.DestinationType,
                    account.MaskedAccountIdentifier,
                    account.Currency,
                    account.BankCode,
                    account.MobileNetwork,
                    recipient.RelationshipTypeCode,
                    account.IsVerified);
            })
            .ToList();
    }

    private async Task<(Guid PartyId, string DisplayName)> ResolveBeneficiaryPartyAsync(
        SavePayoutBeneficiaryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BeneficiaryPartyId is { } existingPartyId && existingPartyId != Guid.Empty)
        {
            var party = await _partyService.GetPartyAsync(existingPartyId, cancellationToken)
                ?? throw new InvalidOperationException($"Beneficiary party {existingPartyId} not found.");

            return (party.PartyId, party.DisplayName);
        }

        var displayName = !string.IsNullOrWhiteSpace(request.BeneficiaryDisplayName)
            ? request.BeneficiaryDisplayName.Trim()
            : request.AccountName.Trim();

        var partyType = string.IsNullOrWhiteSpace(request.BeneficiaryPartyType)
            ? "Person"
            : request.BeneficiaryPartyType.Trim();

        var created = await _partyService.CreatePartyAsync(
            new CreatePartyRequest(displayName, partyType, null, null, null, null, null),
            cancellationToken);

        return (created.PartyId, created.DisplayName);
    }

    private async Task EnsureRelationshipAsync(
        Guid customerPartyId,
        Guid beneficiaryPartyId,
        string relationshipTypeCode,
        string? notes,
        CancellationToken cancellationToken)
    {
        var relationships = await _partyService.GetRelationshipsAsync(customerPartyId, cancellationToken);

        var alreadyLinked = relationships.Any(relationship =>
            relationship.FromPartyId == customerPartyId
            && relationship.ToPartyId == beneficiaryPartyId
            && string.Equals(relationship.RelationshipTypeCode, relationshipTypeCode, StringComparison.OrdinalIgnoreCase));

        if (alreadyLinked)
        {
            return;
        }

        await _partyService.CreateRelationshipAsync(
            new CreatePartyRelationshipRequest(customerPartyId, beneficiaryPartyId, relationshipTypeCode, notes),
            cancellationToken);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
