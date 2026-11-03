using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Services.Payments;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Finance.Services.Payments;

internal sealed partial class PayoutBeneficiaryService
{
    public async Task<OperatorBeneficiaryListResponse> ListForCustomerAsync(Guid customerPartyId, CancellationToken cancellationToken = default)
    {
        await AuthorizeOperatorAsync(customerPartyId, false, cancellationToken);
        var destinations = await ListBeneficiariesAsync(customerPartyId, cancellationToken);
        var relationships = await _partyService.GetRelationshipsAsync(customerPartyId, cancellationToken);
        var notes = relationships.Where(edge => edge.FromPartyId == customerPartyId && edge.IsActive)
            .GroupBy(edge => edge.ToPartyId)
            .ToDictionary(group => group.Key, group => group.FirstOrDefault(edge => edge.RelationshipTypeCode == PartyRelationshipTypeCodes.Recipient)?.Notes);
        return new OperatorBeneficiaryListResponse(customerPartyId, destinations
            .Select(destination => new OperatorBeneficiaryResponse(destination, notes.GetValueOrDefault(destination.BeneficiaryPartyId))).ToList());
    }

    public async Task<PayoutBeneficiaryResponse> RegisterForCustomerAsync(VerifyPayoutBeneficiaryRequest request, CancellationToken cancellationToken = default)
    {
        await AuthorizeOperatorAsync(request.CustomerPartyId, true, cancellationToken);
        if (request.BeneficiaryPartyId.HasValue)
        {
            var tenantId = _tenantProvider.GetCurrentTenantId();
            if (!await _dbContext.Parties.AsNoTracking().AnyAsync(party => party.Id == request.BeneficiaryPartyId.Value && party.TenantId == tenantId, cancellationToken))
                throw new NotFoundException("Beneficiary not found.");
        }

        // Staff use the same verification/registration pipeline without acquiring a UserParty link.
        // Recipient is the neutral payable edge; this surface cannot author privileged family edges.
        return await RegisterCoreAsync(request with { RelationshipTypeCode = PartyRelationshipTypeCodes.Recipient }, cancellationToken);
    }

    public async Task UpdateNotesAsync(Guid customerPartyId, Guid beneficiaryPartyId, string notes, CancellationToken cancellationToken = default)
    {
        await AuthorizeOperatorAsync(customerPartyId, true, cancellationToken);
        if (notes is null || notes.Length > 1024) throw new ArgumentException("Recipient notes must be at most 1024 characters.", nameof(notes));
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!await _dbContext.ExternalPayoutAccounts.AsNoTracking().AnyAsync(account => account.TenantId == tenantId &&
            account.CustomerPartyId == customerPartyId && account.BeneficiaryPartyId == beneficiaryPartyId, cancellationToken))
            throw new NotFoundException("Beneficiary not found for this customer.");

        var relationships = await _partyService.GetRelationshipsAsync(customerPartyId, cancellationToken);
        var edge = relationships.FirstOrDefault(edge => edge.FromPartyId == customerPartyId && edge.ToPartyId == beneficiaryPartyId &&
            edge.IsActive && edge.RelationshipTypeCode == PartyRelationshipTypeCodes.Recipient)
            ?? throw new NotFoundException("Recipient relationship not found for this customer.");
        if (!await _partyService.UpdateRelationshipAsync(edge.RelationshipId, notes: notes.Trim(), cancellationToken: cancellationToken))
            throw new NotFoundException("Recipient relationship not found for this customer.");
    }

    public async Task RemoveDestinationAsync(Guid customerPartyId, Guid destinationId, CancellationToken cancellationToken = default)
    {
        await AuthorizeOperatorAsync(customerPartyId, true, cancellationToken);
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var account = await _dbContext.ExternalPayoutAccounts.FirstOrDefaultAsync(account => account.Id == destinationId &&
            account.TenantId == tenantId && account.CustomerPartyId == customerPartyId, cancellationToken);
        if (account is null) return; // Idempotent and does not disclose another customer's destination.

        // Preserve the party, other rails and historical references. The shared audit interceptor soft-deletes.
        _dbContext.ExternalPayoutAccounts.Remove(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AuthorizeOperatorAsync(Guid customerPartyId, bool write, CancellationToken cancellationToken)
    {
        var userId = _currentUserProvider.GetCurrentUserId();
        foreach (var permission in write ? new[] { "Payment.Create", "Customers.Write" } : new[] { "Payment.Read", "Customers.Read" })
        {
            if (!userId.HasValue || userId.Value == Guid.Empty || !await _permissionService.HasPermissionAsync(userId.Value, permission, cancellationToken))
                throw new PermissionDeniedException(permission);
        }
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (customerPartyId == Guid.Empty || !await _dbContext.Parties.AsNoTracking().AnyAsync(party => party.Id == customerPartyId && party.TenantId == tenantId, cancellationToken))
            throw new NotFoundException("Customer not found.");
    }
}
