namespace Aonik.SharedKernel.Abstractions.Messaging;

/// <summary>Tenant-owned customer email contacts and published business identity for receipt delivery.</summary>
public interface ICustomerReceiptContactReader
{
    Task<CustomerReceiptContacts?> GetAsync(Guid customerPartyId, CancellationToken cancellationToken = default);
}

public record ReceiptEmailContact(Guid ContactId, string Email, bool IsPrimary);
public record CustomerReceiptContacts(string CustomerName, string BusinessName, IReadOnlyList<ReceiptEmailContact> Contacts);
