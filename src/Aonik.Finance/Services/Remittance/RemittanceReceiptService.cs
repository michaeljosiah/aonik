using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Models.Remittance;
using Aonik.Finance.Contracts.Services.Remittance;
using Aonik.Finance.Entities.Orders;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Finance.Services.Remittance;

internal sealed class RemittanceReceiptService(
    FinanceDbContext db, IOperatorRemittanceService transfers, IRemittanceReceiptRenderer renderer,
    ICustomerReceiptContactReader contacts, ICustomerReceiptContactEditor contactEditor, ITemplatedEmailSender email, ITenantProvider tenantProvider,
    ICurrentUserProvider currentUser, IPermissionService permissions, IClock clock) : IRemittanceReceiptService
{
    private const string RequestedEvent = "RemittanceReceiptEmailRequested";
    private const string ResultEvent = "RemittanceReceiptEmailResult";

    public async Task<RemittanceReceiptFile> DownloadAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var transfer = await ReadTransferAsync(orderId, cancellationToken);
        var customer = await contacts.GetAsync(transfer.CustomerPartyId, cancellationToken);
        return renderer.Render(transfer, customer?.BusinessName ?? "AONIK", tenantProvider.GetCurrentTenantId(), clock.UtcNow);
    }

    public async Task<ReceiptEmailOptions> GetEmailOptionsAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync("Customers.Read", cancellationToken);
        await RequirePermissionAsync("Customers.Write", cancellationToken);
        var transfer = await ReadTransferAsync(orderId, cancellationToken);
        var customer = await contacts.GetAsync(transfer.CustomerPartyId, cancellationToken) ?? throw new NotFoundException("Customer not found.");
        return new ReceiptEmailOptions(customer.CustomerName, transfer.OrderNumber, RemittanceReceiptContent.Status(transfer), RemittanceReceiptContent.FileName(transfer), customer.Contacts);
    }

    public async Task<ReceiptEmailResult> SendAsync(Guid orderId, SendReceiptEmailRequest request, CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync("Customers.Read", cancellationToken);
        await RequirePermissionAsync("Customers.Write", cancellationToken);
        var transfer = await ReadTransferAsync(orderId, cancellationToken);
        if (!request.ConfirmSend || request.RequestId == Guid.Empty || request.ContactId == Guid.Empty)
            throw new ArgumentException("Explicit receipt email confirmation and request/contact references are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ExpectedEmail);
        var tenantId = tenantProvider.GetCurrentTenantId();
        var existing = await FindRequestAsync(orderId, request.RequestId, cancellationToken);
        if (existing is not null)
        {
            var original = ReadDetails(existing);
            if (original.ContactId != request.ContactId || original.EmailHash != Hash(request.ExpectedEmail.Trim().ToLowerInvariant()))
                throw new InvalidStateException("This send reference is already bound to another recipient.");
            return await ResultAsync(existing, cancellationToken);
        }

        var customer = await contacts.GetAsync(transfer.CustomerPartyId, cancellationToken) ?? throw new NotFoundException("Customer not found.");
        var contact = customer.Contacts.SingleOrDefault(contact => contact.ContactId == request.ContactId)
            ?? throw new InvalidStateException("Choose a saved email address belonging to this customer.");
        if (!string.Equals(contact.Email, request.ExpectedEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidStateException("The customer's email address changed. Review the recipient again before sending.");

        var generatedAt = clock.UtcNow;
        var file = renderer.Render(transfer, customer.BusinessName, tenantId, generatedAt);
        var details = new ReceiptEmailRequestDetails(contact.ContactId, Hash(contact.Email.ToLowerInvariant()), Hash(file.Content), Mask(contact.Email), file.FileName, DeliveryTrackingEnabled: true);
        var claim = new OrderHistoryEvent
        {
            Id = request.RequestId, TenantId = tenantId, OrderId = orderId, EventType = RequestedEvent,
            EventAt = generatedAt, ActorType = "User", ActorId = currentUser.GetCurrentUserId()!.Value,
            DetailsJson = JsonSerializer.Serialize(details)
        };
        db.OrderHistoryEvents.Add(claim);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            db.Entry(claim).State = EntityState.Detached;
            var winner = await FindRequestAsync(orderId, request.RequestId, cancellationToken);
            if (winner is null) throw new InvalidStateException("This send reference could not be reserved. Reload its status before trying again.");
            var original = ReadDetails(winner);
            if (original.ContactId != request.ContactId || original.EmailHash != details.EmailHash) throw new InvalidStateException("This send reference is already bound to another recipient.");
            return await ResultAsync(winner, cancellationToken);
        }

        var status = "Accepted";
        try
        {
            await email.SendAsync(new TemplatedEmailMessage(TransactionalEmailTemplateNames.RemittanceReceipt, contact.Email,
                new Dictionary<string, object?>
                {
                    ["customer_name"] = customer.CustomerName, ["order_number"] = transfer.OrderNumber,
                    ["status"] = RemittanceReceiptContent.Status(transfer), ["status_description"] = RemittanceReceiptContent.StatusDescription(transfer),
                    ["send_amount"] = RemittanceReceiptContent.Amount(transfer.OriginAmount, transfer.OriginCurrency),
                    ["fees"] = RemittanceReceiptContent.Amount(transfer.FeesTotal, transfer.OriginCurrency),
                    ["total_debit"] = RemittanceReceiptContent.Amount(transfer.TotalAmount, transfer.OriginCurrency),
                    ["recipient_amount"] = RemittanceReceiptContent.Amount(transfer.DestinationAmount, transfer.DestinationCurrency),
                    ["destination"] = transfer.MaskedAccountIdentifier, ["generated_at"] = RemittanceReceiptContent.Timestamp(generatedAt)
                }, [new EmailAttachment(file.FileName, "application/pdf", file.Content)], TrackingId: request.RequestId), cancellationToken);
        }
        catch (EmailDeliveryNotStartedException) { status = "Failed"; }
        catch (Exception) { status = "Unknown"; } // The provider may have accepted it; never retry implicitly.

        // Append the outcome rather than changing the immutable request/audit evidence. Persist even
        // when the caller disconnected; a failed commit still leaves a non-replayable Sending claim.
        db.OrderHistoryEvents.Add(new OrderHistoryEvent
        {
            Id = OutcomeId(request.RequestId), TenantId = tenantId, OrderId = orderId, EventType = ResultEvent,
            EventAt = clock.UtcNow, ActorType = "System", ActorId = Guid.Empty,
            DetailsJson = JsonSerializer.Serialize(new { RequestId = request.RequestId, Status = status })
        });
        await db.SaveChangesAsync(CancellationToken.None);
        return await ResultAsync(claim, CancellationToken.None);
    }

    public async Task<ReceiptEmailResult?> GetSendStatusAsync(Guid orderId, Guid requestId, CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync("Customers.Read", cancellationToken);
        await RequirePermissionAsync("Customers.Write", cancellationToken);
        _ = await ReadTransferAsync(orderId, cancellationToken);
        var request = await FindRequestAsync(orderId, requestId, cancellationToken);
        return request is null ? null : await ResultAsync(request, cancellationToken);
    }

    public async Task<ReceiptEmailHistory> GetEmailHistoryAsync(Guid orderId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await RequirePermissionAsync("Customers.Read", cancellationToken);
        _ = await ReadTransferAsync(orderId, cancellationToken);
        if (pageNumber < 1 || pageSize is < 1 or > 50 || (long)(pageNumber - 1) * pageSize > int.MaxValue)
            throw new ArgumentException("Choose a valid history page and a page size between 1 and 50.");

        var tenantId = tenantProvider.GetCurrentTenantId();
        var query = db.OrderHistoryEvents.AsNoTracking().Where(item => item.TenantId == tenantId && item.OrderId == orderId && item.EventType == RequestedEvent);
        var count = await query.CountAsync(cancellationToken);
        var requests = await query.OrderByDescending(item => item.EventAt).ThenByDescending(item => item.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var outcomeIds = requests.Select(request => OutcomeId(request.Id)).ToArray();
        var outcomes = await db.OrderHistoryEvents.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.OrderId == orderId && item.EventType == ResultEvent && outcomeIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var reports = await DeliveryReportsAsync(orderId, requests.Select(request => request.Id).ToArray(), cancellationToken);
        var reportsByRequest = reports.ToLookup(report => ReceiptEmailDelivery.DeliveryDetails(report).RequestId);
        var items = requests.Select(request =>
        {
            var details = ReadDetails(request);
            outcomes.TryGetValue(OutcomeId(request.Id), out var outcome);
            var sendStatus = OutcomeStatus(outcome);
            var delivery = ReceiptEmailDelivery.Project(request, sendStatus, reportsByRequest[request.Id]);
            return new ReceiptEmailHistoryItem(request.Id, details.MaskedRecipient, details.FileName,
                DateTime.SpecifyKind(request.EventAt, DateTimeKind.Utc), delivery.HasReport ? "Accepted" : sendStatus,
                delivery.HasReport && sendStatus != "Accepted" ? delivery.UpdatedAt : outcome is null ? null : DateTime.SpecifyKind(outcome.EventAt, DateTimeKind.Utc), delivery.Status, delivery.UpdatedAt);
        }).ToList();
        return new ReceiptEmailHistory(items, count, pageNumber, pageSize);
    }

    public async Task<ReceiptRecipientReview> GetBouncedRecipientAsync(Guid orderId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var (transfer, original) = await BouncedRequestAsync(orderId, requestId, cancellationToken);
        var customer = await contacts.GetAsync(transfer.CustomerPartyId, cancellationToken) ?? throw new NotFoundException("Customer not found.");
        var contact = customer.Contacts.SingleOrDefault(c => c.ContactId == original.ContactId)
            ?? throw new NotFoundException("The original saved email contact is no longer available. Choose another saved recipient when resending.");
        return new(customer.CustomerName, transfer.OrderNumber, contact, original.EmailHash != Hash(contact.Email.ToLowerInvariant()));
    }

    public async Task<ReceiptEmailContact> UpdateBouncedRecipientAsync(Guid orderId, Guid requestId, UpdateReceiptRecipientRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.ConfirmUpdate) throw new ArgumentException("Confirm the saved customer email update.");
        var (transfer, original) = await BouncedRequestAsync(orderId, requestId, cancellationToken);
        return await contactEditor.UpdateEmailAsync(transfer.CustomerPartyId, original.ContactId, request.ExpectedEmail,
            request.NewEmail, requestId, cancellationToken);
    }

    private async Task<(RemittanceOrderResponse Transfer, ReceiptEmailRequestDetails Original)> BouncedRequestAsync(Guid orderId, Guid requestId, CancellationToken ct)
    {
        await RequirePermissionAsync("Customers.Read", ct);
        await RequirePermissionAsync("Customers.Write", ct);
        var transfer = await ReadTransferAsync(orderId, ct);
        var request = await FindRequestAsync(orderId, requestId, ct) ?? throw new NotFoundException("Receipt email request not found.");
        if ((await ResultAsync(request, ct)).DeliveryStatus != "Bounced")
            throw new InvalidStateException("Review email is available for a currently bounced receipt. Refresh its delivery history.");
        return (transfer, ReadDetails(request));
    }

    private async Task<RemittanceOrderResponse> ReadTransferAsync(Guid orderId, CancellationToken ct)
    {
        await RequirePermissionAsync("Payment.Read", ct);
        return await transfers.GetForOperatorAsync(orderId, ct) ?? throw new NotFoundException("Transfer not found.");
    }

    private async Task RequirePermissionAsync(string permission, CancellationToken ct)
    {
        var actor = currentUser.GetCurrentUserId();
        if (!actor.HasValue || actor == Guid.Empty || !await permissions.HasPermissionAsync(actor.Value, permission, ct)) throw new PermissionDeniedException(permission);
    }

    private Task<OrderHistoryEvent?> FindRequestAsync(Guid orderId, Guid requestId, CancellationToken ct) => db.OrderHistoryEvents.AsNoTracking()
        .SingleOrDefaultAsync(item => item.Id == requestId && item.TenantId == tenantProvider.GetCurrentTenantId() && item.OrderId == orderId && item.EventType == RequestedEvent, ct);

    private async Task<ReceiptEmailResult> ResultAsync(OrderHistoryEvent request, CancellationToken ct)
    {
        var details = ReadDetails(request);
        var outcomeId = OutcomeId(request.Id);
        var outcome = await db.OrderHistoryEvents.AsNoTracking().SingleOrDefaultAsync(item => item.Id == outcomeId && item.TenantId == request.TenantId && item.OrderId == request.OrderId && item.EventType == ResultEvent, ct);
        var reports = await DeliveryReportsAsync(request.OrderId, [request.Id], ct);
        var delivery = ReceiptEmailDelivery.Project(request, OutcomeStatus(outcome), reports.Where(report => ReceiptEmailDelivery.DeliveryDetails(report).RequestId == request.Id));
        return new ReceiptEmailResult(request.Id, delivery.HasReport ? "Accepted" : OutcomeStatus(outcome), details.MaskedRecipient, details.FileName,
            DateTime.SpecifyKind(request.EventAt, DateTimeKind.Utc), delivery.Status, delivery.UpdatedAt);
    }

    private Task<List<OrderHistoryEvent>> DeliveryReportsAsync(Guid orderId, Guid[] requestIds, CancellationToken ct)
    {
        var eventTypes = requestIds.Select(ReceiptEmailDelivery.EventType).ToArray();
        return db.OrderHistoryEvents.AsNoTracking().Where(item => item.TenantId == tenantProvider.GetCurrentTenantId() &&
            item.OrderId == orderId && eventTypes.Contains(item.EventType)).ToListAsync(ct);
    }

    private static string OutcomeStatus(OrderHistoryEvent? outcome)
    {
        using var details = outcome is null ? null : JsonDocument.Parse(outcome.DetailsJson);
        return details is null ? "Sending" : details.RootElement.GetProperty("Status").GetString()!;
    }

    private static ReceiptEmailRequestDetails ReadDetails(OrderHistoryEvent request) => ReceiptEmailDelivery.RequestDetails(request);
    private static Guid OutcomeId(Guid requestId) => new(SHA256.HashData(Encoding.UTF8.GetBytes($"receipt-email-result:{requestId:D}"))[..16]);
    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private static string Mask(string address) { var at = address.LastIndexOf('@'); return at > 0 ? $"{address[0]}***{address[at..]}" : "saved customer email"; }
}
