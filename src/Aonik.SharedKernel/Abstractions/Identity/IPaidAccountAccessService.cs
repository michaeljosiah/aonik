namespace Aonik.SharedKernel.Abstractions.Identity;

/// <summary>Durably issues one account-access action for an opted-in, confirmed guest checkout.</summary>
public interface IPaidAccountAccessService
{
    Task IssueAsync(PaidAccountAccessRequest request, CancellationToken cancellationToken = default);
}

public sealed record PaidAccountAccessRequest(
    Guid TenantId,
    Guid CartId,
    Guid OrderId,
    Guid PaymentIntentId,
    Guid GuestPartyId,
    string Email);
