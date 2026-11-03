using Aonik.Finance.Contracts.Models.Remittance;
using Aonik.SharedKernel.Abstractions.Messaging;

namespace Aonik.Finance.Contracts.Services.Remittance;

public interface IRemittanceReceiptService
{
    Task<RemittanceReceiptFile> DownloadAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<ReceiptEmailOptions> GetEmailOptionsAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<ReceiptEmailResult> SendAsync(Guid orderId, SendReceiptEmailRequest request, CancellationToken cancellationToken = default);
    Task<ReceiptEmailResult?> GetSendStatusAsync(Guid orderId, Guid requestId, CancellationToken cancellationToken = default);
    Task<ReceiptEmailHistory> GetEmailHistoryAsync(Guid orderId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<ReceiptRecipientReview> GetBouncedRecipientAsync(Guid orderId, Guid requestId, CancellationToken cancellationToken = default);
    Task<ReceiptEmailContact> UpdateBouncedRecipientAsync(Guid orderId, Guid requestId, UpdateReceiptRecipientRequest request, CancellationToken cancellationToken = default);
}

public interface IRemittanceReceiptRenderer
{
    RemittanceReceiptFile Render(RemittanceOrderResponse transfer, string businessName, Guid tenantId, DateTime generatedAt);
}

public record RemittanceReceiptFile(string FileName, byte[] Content);
public record ReceiptEmailOptions(string CustomerName, string OrderNumber, string TransferStatus, string FileName, IReadOnlyList<ReceiptEmailContact> Contacts);
public record SendReceiptEmailRequest(Guid RequestId, Guid ContactId, string ExpectedEmail, bool ConfirmSend);
public record ReceiptEmailResult(Guid RequestId, string Status, string MaskedRecipient, string FileName, DateTime RequestedAt, string DeliveryStatus = "NotTracked", DateTime? DeliveryUpdatedAt = null);
public record ReceiptEmailHistoryItem(Guid RequestId, string MaskedRecipient, string FileName, DateTime RequestedAt, string Status, DateTime? StatusUpdatedAt, string DeliveryStatus = "NotTracked", DateTime? DeliveryUpdatedAt = null);
public record ReceiptEmailHistory(IReadOnlyList<ReceiptEmailHistoryItem> Items, int TotalCount, int PageNumber, int PageSize);
public record ReceiptRecipientReview(string CustomerName, string OrderNumber, ReceiptEmailContact Contact, bool ChangedSinceSend);
public record UpdateReceiptRecipientRequest(string ExpectedEmail, string NewEmail, bool ConfirmUpdate);
