namespace Aonik.SharedKernel.Abstractions.Messaging;

/// <summary>Updates a tenant-owned saved customer contact without sending email.</summary>
public interface ICustomerReceiptContactEditor
{
    Task<ReceiptEmailContact> UpdateEmailAsync(Guid customerPartyId, Guid contactId, string expectedEmail,
        string newEmail, Guid receiptRequestId, CancellationToken cancellationToken = default);
}
