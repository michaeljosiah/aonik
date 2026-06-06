using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.Finance.Entities.Payments;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Services.Payments;

/// <summary>
/// Façade over the recipient surface. Composes the shipped <see cref="IPayoutBeneficiaryService"/>
/// (which owns the party + edge + role + rail stitching) with the cross-module party seam
/// (<see cref="IPartyService"/>) for relationship reads/updates and photos. It adds no storage of its
/// own: a recipient is projected from the customer's relationship edges joined to the Finance rails.
///
/// A recipient appears on this surface only when it is genuinely payable — an owned, active edge with
/// at least one saved <see cref="ExternalPayoutAccount"/>. That excludes plain kinship/household edges
/// that were never set up as payout destinations, and makes soft-removal (rails deleted) self-hiding.
/// </summary>
internal sealed class RecipientService : IRecipientService
{
    private const int MaxPageSize = 100;

    private readonly FinanceDbContext _dbContext;
    private readonly IPartyService _partyService;
    private readonly IPayoutBeneficiaryService _payoutBeneficiaryService;
    private readonly ITenantProvider _tenantProvider;

    public RecipientService(
        FinanceDbContext dbContext,
        IPartyService partyService,
        IPayoutBeneficiaryService payoutBeneficiaryService,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _partyService = partyService;
        _payoutBeneficiaryService = payoutBeneficiaryService;
        _tenantProvider = tenantProvider;
    }

    public async Task<RecipientResponse> CreateAsync(
        SavePayoutBeneficiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var saved = await _payoutBeneficiaryService.SaveBeneficiaryAsync(request, cancellationToken);

        return await GetAsync(request.CustomerPartyId, saved.BeneficiaryPartyId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Recipient {saved.BeneficiaryPartyId} could not be projected after save.");
    }

    public async Task<RecipientResponse?> GetAsync(
        Guid customerPartyId,
        Guid recipientPartyId,
        CancellationToken cancellationToken = default)
    {
        var recipients = await ProjectRecipientsAsync(customerPartyId, cancellationToken);
        return recipients.FirstOrDefault(recipient => recipient.RecipientPartyId == recipientPartyId);
    }

    public async Task<RecipientListResponse> ListAsync(
        Guid customerPartyId,
        RecipientQuery query,
        CancellationToken cancellationToken = default)
    {
        var recipients = await ProjectRecipientsAsync(customerPartyId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            recipients = recipients
                .Where(recipient => recipient.DisplayName.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var total = recipients.Count;

        var pageItems = recipients
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new RecipientListResponse(customerPartyId, page, pageSize, total, pageItems);
    }

    public async Task<RecipientResponse> UpdateAsync(
        Guid customerPartyId,
        Guid recipientPartyId,
        UpdateRecipientRequest request,
        CancellationToken cancellationToken = default)
    {
        var edge = await ResolveOwnedEdgeAsync(customerPartyId, recipientPartyId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Recipient {recipientPartyId} not found for customer {customerPartyId}.");

        await _partyService.UpdateRelationshipAsync(
            edge.RelationshipId,
            request.RelationshipTypeCode,
            request.Notes,
            isActive: null,
            cancellationToken);

        return await GetAsync(customerPartyId, recipientPartyId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Recipient {recipientPartyId} could not be projected after update.");
    }

    public async Task RemoveAsync(
        Guid customerPartyId,
        Guid recipientPartyId,
        CancellationToken cancellationToken = default)
    {
        if (customerPartyId == Guid.Empty)
        {
            throw new ArgumentException("Customer party id is required.", nameof(customerPartyId));
        }

        if (recipientPartyId == Guid.Empty)
        {
            throw new ArgumentException("Recipient party id is required.", nameof(recipientPartyId));
        }

        // 1) Resolve ownership FIRST. If this customer does not own an active edge to the recipient,
        //    there is nothing to remove — and we must never touch the rails of another customer who
        //    happens to share the same payee party. A non-owner's call is a silent no-op (idempotent,
        //    leaks nothing).
        var relationships = await _partyService.GetRelationshipsAsync(customerPartyId, cancellationToken);
        var ownsRecipient = relationships.Any(relationship =>
            relationship.FromPartyId == customerPartyId
            && relationship.ToPartyId == recipientPartyId
            && relationship.IsActive);

        if (!ownsRecipient)
        {
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        // 2) Soft-delete ONLY this customer's rails for the recipient — scoped by CustomerPartyId, never
        //    by beneficiary party alone. Remove() on an AuditableEntity becomes a soft-delete (IsDeleted)
        //    via the SaveChanges interceptor, so the rail disappears from every query without being lost.
        var rails = await _dbContext.ExternalPayoutAccounts
            .Where(account => account.TenantId == tenantId
                              && account.CustomerPartyId == customerPartyId
                              && account.BeneficiaryPartyId == recipientPartyId)
            .ToListAsync(cancellationToken);

        if (rails.Count > 0)
        {
            _dbContext.ExternalPayoutAccounts.RemoveRange(rails);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 3) Deactivate this customer's Recipient edge. Kinship edges (Mother, Spouse, …) and any other
        //    customer's edge to the same party are left intact — removing someone as a payout recipient
        //    does not unmake them as a relative, nor remove them for a different customer.
        var recipientEdge = relationships.FirstOrDefault(relationship =>
            relationship.FromPartyId == customerPartyId
            && relationship.ToPartyId == recipientPartyId
            && relationship.IsActive
            && relationship.RelationshipTypeCode == PartyRelationshipTypeCodes.Recipient);

        if (recipientEdge is not null)
        {
            await _partyService.UpdateRelationshipAsync(
                recipientEdge.RelationshipId,
                isActive: false,
                cancellationToken: cancellationToken);
        }
    }

    public async Task<RecipientPhotoResponse> UploadPhotoAsync(
        Guid customerPartyId,
        Guid recipientPartyId,
        string contentType,
        Stream photo,
        CancellationToken cancellationToken = default)
    {
        // Ownership check before mutating a shared party's photo: the caller must own this recipient.
        _ = await ResolveOwnedEdgeAsync(customerPartyId, recipientPartyId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Recipient {recipientPartyId} not found for customer {customerPartyId}.");

        var urls = await _partyService.SetPartyPhotoAsync(recipientPartyId, contentType, photo, cancellationToken);

        return new RecipientPhotoResponse(
            recipientPartyId,
            urls.PhotoUrl,
            urls.PhotoUrlMedium,
            urls.PhotoUrlSmall,
            urls.PhotoUrlTiny);
    }

    /// <summary>
    /// Projects the customer's payable recipients: owned active edges joined to their saved rails,
    /// enriched with the recipient's photo. Ordered by display name for a stable list.
    /// </summary>
    private async Task<List<RecipientResponse>> ProjectRecipientsAsync(
        Guid customerPartyId,
        CancellationToken cancellationToken)
    {
        if (customerPartyId == Guid.Empty)
        {
            throw new ArgumentException("Customer party id is required.", nameof(customerPartyId));
        }

        var relationships = await _partyService.GetRelationshipsAsync(customerPartyId, cancellationToken);

        // Outgoing, active edges from this customer identify the recipients they own.
        var edgeByRecipient = relationships
            .Where(relationship => relationship.FromPartyId == customerPartyId && relationship.IsActive)
            .GroupBy(relationship => relationship.ToPartyId)
            .ToDictionary(group => group.Key, PickDisplayEdge);

        if (edgeByRecipient.Count == 0)
        {
            return new List<RecipientResponse>();
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var recipientPartyIds = edgeByRecipient.Keys.ToList();

        var rails = await _dbContext.ExternalPayoutAccounts
            .AsNoTracking()
            .Where(account => account.TenantId == tenantId
                              && account.CustomerPartyId == customerPartyId
                              && account.BeneficiaryPartyId != null
                              && recipientPartyIds.Contains(account.BeneficiaryPartyId.Value))
            .OrderByDescending(account => account.CreatedAt)
            .ToListAsync(cancellationToken);

        if (rails.Count == 0)
        {
            return new List<RecipientResponse>();
        }

        var photosByPartyId = (await _partyService.GetPartyPhotosAsync(recipientPartyIds, cancellationToken))
            .ToDictionary(photo => photo.PartyId);

        return rails
            .GroupBy(account => account.BeneficiaryPartyId!.Value)
            .Where(group => edgeByRecipient.ContainsKey(group.Key))
            .Select(group =>
            {
                var edge = edgeByRecipient[group.Key];
                photosByPartyId.TryGetValue(group.Key, out var photo);

                return new RecipientResponse(
                    group.Key,
                    edge.ToPartyName,
                    edge.RelationshipTypeCode,
                    photo?.PhotoUrl,
                    photo?.PhotoUrlSmall,
                    edge.IsActive,
                    group.Select(MapRail).ToList());
            })
            .OrderBy(recipient => recipient.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<PartyRelationshipResponse?> ResolveOwnedEdgeAsync(
        Guid customerPartyId,
        Guid recipientPartyId,
        CancellationToken cancellationToken)
    {
        var relationships = await _partyService.GetRelationshipsAsync(customerPartyId, cancellationToken);

        var ownedEdges = relationships
            .Where(relationship =>
                relationship.FromPartyId == customerPartyId
                && relationship.ToPartyId == recipientPartyId
                && relationship.IsActive)
            .ToList();

        return PickDisplayEdgeOrDefault(ownedEdges);
    }

    /// <summary>Prefer the neutral <c>Recipient</c> edge for display; fall back to the first edge.</summary>
    private static PartyRelationshipResponse PickDisplayEdge(IEnumerable<PartyRelationshipResponse> edges)
        => PickDisplayEdgeOrDefault(edges)!;

    private static PartyRelationshipResponse? PickDisplayEdgeOrDefault(IEnumerable<PartyRelationshipResponse> edges)
    {
        var materialized = edges as IReadOnlyList<PartyRelationshipResponse> ?? edges.ToList();
        return materialized.FirstOrDefault(edge => edge.RelationshipTypeCode == PartyRelationshipTypeCodes.Recipient)
               ?? materialized.FirstOrDefault();
    }

    private static RecipientRailResponse MapRail(ExternalPayoutAccount account)
        => new(
            account.Id,
            account.DestinationType,
            account.AccountName,
            account.MaskedAccountIdentifier,
            account.Currency,
            account.BankCode,
            account.MobileNetwork,
            account.IsVerified);
}
