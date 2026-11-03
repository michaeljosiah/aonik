using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Compliance;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Services.Notifications;

internal sealed class CustomerReceiptContactEditor(PlatformDbContext db, ITenantProvider tenantProvider,
    ICurrentUserProvider currentUser, IPermissionService permissions, IAuditLogWriter audit, IClock clock)
    : ICustomerReceiptContactEditor
{
    public async Task<ReceiptEmailContact> UpdateEmailAsync(Guid customerPartyId, Guid contactId, string expectedEmail,
        string newEmail, Guid receiptRequestId, CancellationToken cancellationToken = default)
    {
        var actor = currentUser.GetCurrentUserId();
        foreach (var permission in new[] { "Customers.Read", "Customers.Write" })
            if (actor is null || actor == Guid.Empty || !await permissions.HasPermissionAsync(actor.Value, permission, cancellationToken))
                throw new PermissionDeniedException(permission);
        var normalized = newEmail?.Trim() ?? "";
        if (normalized.Length > 320 || !MailAddress.TryCreate(normalized, out var address) || address.Address != normalized || receiptRequestId == Guid.Empty)
            throw new ArgumentException("Enter a valid customer email address.");
        var tenantId = tenantProvider.GetCurrentTenantId();
        var customer = await db.Parties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == customerPartyId && p.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");
        var contact = await db.PartyContacts.SingleOrDefaultAsync(c => c.Id == contactId && c.PartyId == customer.Id && c.Type == "Email", cancellationToken)
            ?? throw new NotFoundException("The original saved email contact is no longer available. Choose another saved recipient when resending.");
        if (!string.Equals(contact.Value.Trim(), expectedEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidStateException("The saved email changed. Reload and review it before updating.");
        if (string.Equals(contact.Value.Trim(), normalized, StringComparison.OrdinalIgnoreCase))
            return new(contact.Id, contact.Value.Trim(), contact.IsPrimary);
        if (await db.PartyContacts.AnyAsync(c => c.PartyId == customer.Id && c.Type == "Email" && c.Id != contactId && c.Value.Trim().ToLower() == normalized.ToLower(), cancellationToken))
            throw new InvalidStateException("This customer already has that saved email. Choose it when resending instead.");

        var previous = contact.Value;
        var existingAuditEntries = db.ChangeTracker.Entries<AuditLog>().Select(entry => entry.Entity).ToHashSet();
        contact.Value = normalized;
        // The existing contact rowversion makes competing edits conflict. AuditLogWriter shares
        // this Platform context and saves the contact and audit in one transaction.
        contact.UpdatedAt = clock.UtcNow;
        try
        {
            await audit.LogAsync(AuditEventNames.CustomerEmailUpdated, "Party", customer.Id, tenantId, actor, null,
                JsonSerializer.Serialize(new { ContactId = contact.Id, ReceiptRequestId = receiptRequestId,
                    Reason = "BouncedReceiptReview", PreviousEmailHash = Hash(previous), NewEmailHash = Hash(normalized) }), cancellationToken);
        }
        catch (Exception ex)
        {
            db.Entry(contact).State = EntityState.Detached;
            foreach (var entry in db.ChangeTracker.Entries<AuditLog>().Where(entry => !existingAuditEntries.Contains(entry.Entity)).ToList())
                entry.State = EntityState.Detached;
            if (ex is DbUpdateConcurrencyException)
                throw new InvalidStateException("The customer contact changed during this update. Reload and review it again.");
            throw;
        }
        return new(contact.Id, contact.Value, contact.IsPrimary);
    }

    private static string Hash(string email) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));
}
