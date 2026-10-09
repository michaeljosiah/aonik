using System.Globalization;
using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Fulfilment;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Events.Integration;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.GiftCards;

internal sealed class GiftCardDeliveryService(CommerceDbContext db, ITenantProvider tenantProvider,
    IClock clock, IGiftCardService giftCards, ITaskService tasks, ITemplatedEmailSender email,
    ICurrentUserProvider currentUser, IPermissionService permissions) : IGiftCardDeliveryService
{
    public const string ActionType = "commerce.send_gift_card";
    public const string SubjectType = "CommerceGiftCardDelivery";

    public async Task ActivateAsync(GiftCardIssuedEvent issued, CancellationToken cancellationToken = default)
    {
        if (issued.TenantId != tenantProvider.GetCurrentTenantId())
            throw new InvalidStateException("The gift card event does not belong to the current tenant.");
        var source = issued.Source;
        var row = await db.OrderGiftCardDeliveries.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == issued.TenantId
            && value.PaymentIntentId == source.PaymentIntentId, cancellationToken)
            ?? throw new InvalidStateException("The gift card purchase has not been recorded for delivery.");
        row = await LoadAsync(row.Id, cancellationToken);
        if (GiftCardDeliveryData.Source(row) != source)
            throw new InvalidStateException("The gift card event does not match the recorded purchase.");
        var card = await giftCards.GetIssuedAsync(source, cancellationToken)
            ?? throw new InvalidStateException("The gift card has not been issued.");
        ValidateCard(row, card);
        if (card.GiftCardId != issued.GiftCardId)
            throw new InvalidStateException("The gift card event does not match the issued instrument.");
        if (row.GiftCardId is null)
        {
            row.GiftCardId = card.GiftCardId;
            row.MaskedCode = card.MaskedCode;
            row.ExpiresAtUtc = card.ExpiresAtUtc;
            row.Status = "Ready";
            if (row.DeliveryMethod == GiftCardDeliveryMethods.Email)
            {
                row.SendSequence = 1;
                row.SequenceDueAtUtc = row.SendAtUtc;
            }
            try { await SaveAsync(row, cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                row = await LoadAsync(row.Id, cancellationToken);
                if (row.GiftCardId != card.GiftCardId) throw;
            }
        }
        // A crash after the row commit or after task insertion resumes the exact same task ID.
        await ScheduleAsync(row, cancellationToken);
    }

    public async Task<Aonik.Commerce.Contracts.Models.Catalog.PagedResult<SentGiftCardDto>> ListSentAsync(Guid partyId, int page = 1, int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        ValidatePage(page, pageSize);
        var tenantId = tenantProvider.GetCurrentTenantId();
        var query = db.OrderGiftCardDeliveries.AsNoTracking().Where(row => row.TenantId == tenantId && !row.IsDeleted
            && row.GiftCardId != null && db.Carts.Any(cart => cart.TenantId == tenantId && !cart.IsDeleted
                && cart.Id == row.CartId && cart.BuyerPartyId == partyId));
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(row => row.CreatedAt).ThenBy(row => row.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(rows.Select(row =>
        {
            var purchase = GiftCardDeliveryData.Read(row);
            return new SentGiftCardDto(row.Id, row.OrderId, row.DeliveryMethod, purchase.FaceValue, purchase.Currency,
                purchase.RecipientName, MaskEmail(purchase.RecipientEmail), Utc(row.SendAtUtc), row.PostingDate,
                row.Status, Utc(row.LastSentAtUtc), row.MaskedCode, Utc(row.ExpiresAtUtc), CanResend(row));
        }).ToList(), count, page, pageSize);
    }

    public async Task RequestResendAsync(Guid deliveryId, Guid partyId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        if (!await db.OrderGiftCardDeliveries.AsNoTracking().AnyAsync(row => row.TenantId == tenantId && row.Id == deliveryId
            && !row.IsDeleted && row.GiftCardId != null && db.Carts.Any(cart => cart.TenantId == tenantId
                && !cart.IsDeleted && cart.Id == row.CartId && cart.BuyerPartyId == partyId), cancellationToken))
            throw new NotFoundException("The gift card delivery was not found.");
        var row = await LoadAsync(deliveryId, cancellationToken);
        if (row.DeliveryMethod != GiftCardDeliveryMethods.Email || row.SendAtUtc > clock.UtcNow)
            throw new InvalidStateException("Only due email gift cards can be resent.");
        if (row.SentSequence < row.SendSequence)
        {
            var existing = await tasks.GetAsync(GiftCardDeliveryData.TaskId(row.TenantId, row.Id, row.SendSequence), cancellationToken);
            if (existing?.Status is not ("Failed" or "Cancelled"))
            {
                await ScheduleAsync(row, cancellationToken);
                return;
            }
            // A deliberately requested resend can recover an exhausted task. It still uses
            // the original recipient/instrument and consumes the same durable resend budget.
        }
        if (!CanResend(row)) throw new InvalidStateException("This gift card cannot be resent yet. Try again later.");
        var now = clock.UtcNow;
        if (row.ResendWindowStartedAtUtc is null || row.ResendWindowStartedAtUtc <= now.AddDays(-1))
        {
            row.ResendWindowStartedAtUtc = now;
            row.ResendsInWindow = 0;
        }
        row.ResendsInWindow++;
        row.LastResendRequestedAtUtc = now;
        row.SendSequence++;
        row.SequenceDueAtUtc = now;
        row.Status = "Ready";
        await SaveAsync(row, cancellationToken);
        await ScheduleAsync(row, cancellationToken);
    }

    public async Task<TaskActionResult> SendAsync(TaskActionContext context, Guid deliveryId, int sequence,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        if (sequence < 1 || context.TenantId != tenantId || context.SubjectType != SubjectType || context.SubjectId != deliveryId
            || context.AssigneeType != "System" || context.Kind != "ScheduledAction"
            || context.WorkItemId != GiftCardDeliveryData.TaskId(tenantId, deliveryId, sequence))
            return new(TaskActionOutcome.Failed, Error: "The gift card task binding is invalid.");
        var row = await LoadAsync(deliveryId, cancellationToken);
        if (row.DeliveryMethod != GiftCardDeliveryMethods.Email || row.GiftCardId is null
            || sequence > row.SendSequence || context.ScheduledForUtc != row.SequenceDueAtUtc && sequence == row.SendSequence)
            return new(TaskActionOutcome.Failed, Error: "The gift card task does not match the delivery.");
        if (sequence <= row.SentSequence) return new(TaskActionOutcome.Succeeded);
        if (sequence != row.SendSequence || row.SequenceDueAtUtc > clock.UtcNow)
            return new(TaskActionOutcome.Failed, Error: "The gift card delivery is not due.");
        var secret = await giftCards.GetFulfilmentSecretAsync(GiftCardDeliveryData.Source(row), cancellationToken);
        ValidateCard(row, secret.Card);
        EnsureUsable(secret.Card);
        var purchase = GiftCardDeliveryData.Read(row);
        // WorkItemDispatcher owns the renewable execution lease. This row guards the sequence;
        // retries of the same occurrence submit the same instrument, never issue more value.
        row.Status = "Sending";
        db.Entry(row).Property(value => value.Status).IsModified = true;
        await SaveAsync(row, cancellationToken);
        try
        {
            await email.SendAsync(new(TransactionalEmailTemplateNames.GiftCardDelivery, purchase.RecipientEmail!,
                new Dictionary<string, object?>
                {
                    ["recipient_name"] = purchase.RecipientName, ["sender_name"] = purchase.SenderName,
                    ["message"] = purchase.Message, ["gift_code"] = secret.Code,
                    ["face_value"] = purchase.FaceValue.ToString("0.00", CultureInfo.InvariantCulture),
                    ["currency"] = purchase.Currency,
                    ["expires_at"] = secret.Card.ExpiresAtUtc?.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
                    ["terms_version"] = secret.Card.TermsVersion
                }), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch
        {
            // External provider errors can contain an address, rendered body or code. The task
            // dispatcher logs exception details, so return a fixed safe error instead.
            return new(TaskActionOutcome.Failed, Error: "Gift card email submission failed; retry the same delivery.");
        }
        row.Status = "Sent";
        row.SentSequence = sequence;
        row.LastSentAtUtc = clock.UtcNow;
        await SaveAsync(row, cancellationToken);
        return new(TaskActionOutcome.Succeeded);
    }

    public async Task<Aonik.Commerce.Contracts.Models.Catalog.PagedResult<PhysicalGiftCardDto>> ListPhysicalAsync(DateOnly? dueThrough = null,
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await StaffAsync("Customers.Read", cancellationToken);
        ValidatePage(page, pageSize);
        var tenantId = tenantProvider.GetCurrentTenantId();
        var query = db.OrderGiftCardDeliveries.AsNoTracking().Where(row => row.TenantId == tenantId && !row.IsDeleted
            && row.GiftCardId != null && row.DeliveryMethod != GiftCardDeliveryMethods.Email && row.CompletedAtUtc == null);
        if (dueThrough is { } date) query = query.Where(row => row.PostingDate == null || row.PostingDate <= date);
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(row => row.PostingDate).ThenBy(row => row.CreatedAt).ThenBy(row => row.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(rows.Select(Physical).ToList(), count, page, pageSize);
    }

    public async Task<GiftCardPrintDto> PrintAsync(Guid deliveryId, CancellationToken cancellationToken = default)
    {
        var actor = await StaffAsync("Customers.Write", cancellationToken);
        var row = await LoadPhysicalAsync(deliveryId, cancellationToken);
        var secret = await giftCards.GetFulfilmentSecretAsync(GiftCardDeliveryData.Source(row), cancellationToken);
        ValidateCard(row, secret.Card);
        EnsureUsable(secret.Card);
        row.LastPrintedAtUtc = clock.UtcNow;
        row.LastPrintedBy = actor;
        await SaveAsync(row, cancellationToken);
        var purchase = GiftCardDeliveryData.Read(row);
        return new(row.Id, row.OrderId, row.DeliveryMethod, purchase.RecipientName, purchase.PostalAddress,
            purchase.RecipientPhone, purchase.SenderName, purchase.Message, purchase.IncludeGreetingCard,
            purchase.FaceValue, purchase.Currency, secret.Code, secret.Card.ExpiresAtUtc, secret.Card.TermsVersion,
            Convert.ToBase64String(row.RowVersion));
    }

    public async Task<PhysicalGiftCardDto> CompletePhysicalAsync(Guid deliveryId, string expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var actor = await StaffAsync("Customers.Write", cancellationToken);
        var row = await LoadPhysicalAsync(deliveryId, cancellationToken);
        if (row.CompletedAtUtc is not null) return Physical(row);
        if (row.LastPrintedAtUtc is null)
            throw new InvalidStateException("Print the issued gift card before recording physical completion.");
        byte[] version;
        try { version = Convert.FromBase64String(expectedVersion); }
        catch (Exception ex) when (ex is FormatException or ArgumentNullException)
        { throw new DbUpdateConcurrencyException("Supply the current gift card delivery version."); }
        if (!row.RowVersion.SequenceEqual(version)) throw new DbUpdateConcurrencyException("The gift card delivery has changed.");
        row.CompletedAtUtc = clock.UtcNow;
        row.CompletedBy = actor;
        row.Status = row.DeliveryMethod == GiftCardDeliveryMethods.Post ? "Posted" : "Enclosed";
        await SaveAsync(row, cancellationToken);
        return Physical(row);
    }

    private async Task ScheduleAsync(OrderGiftCardDelivery row, CancellationToken ct)
    {
        if (row.DeliveryMethod != GiftCardDeliveryMethods.Email || row.GiftCardId is null || row.SentSequence >= row.SendSequence) return;
        await tasks.ScheduleAsync(new("Deliver purchased gift card", "ScheduledAction", ActionType,
            JsonSerializer.Serialize(new GiftCardDeliveryTaskPayload(row.Id, row.SendSequence)), "System",
            SubjectType: SubjectType, SubjectId: row.Id, RunAtUtc: Utc(row.SequenceDueAtUtc),
            SourceModule: "Commerce", TaskId: GiftCardDeliveryData.TaskId(row.TenantId, row.Id, row.SendSequence)), ct);
    }

    private async Task<OrderGiftCardDelivery> LoadAsync(Guid id, CancellationToken ct)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        foreach (var entry in db.ChangeTracker.Entries<OrderGiftCardDelivery>()
            .Where(entry => entry.Entity.Id == id && entry.Entity.TenantId == tenantId).ToArray()) entry.State = EntityState.Detached;
        return await db.OrderGiftCardDeliveries.SingleOrDefaultAsync(row => row.TenantId == tenantId && row.Id == id && !row.IsDeleted, ct)
            ?? throw new NotFoundException("The gift card delivery was not found.");
    }

    private async Task<OrderGiftCardDelivery> LoadPhysicalAsync(Guid id, CancellationToken ct)
    {
        var row = await LoadAsync(id, ct);
        if (row.GiftCardId is null || row.DeliveryMethod == GiftCardDeliveryMethods.Email)
            throw new NotFoundException("The physical gift card delivery was not found.");
        return row;
    }

    private async Task<Guid> StaffAsync(string permission, CancellationToken ct)
    {
        if (!currentUser.TryGetCurrentUserId(out var actor) || actor == Guid.Empty
            || !await permissions.HasPermissionAsync(actor, permission, ct))
            throw new PermissionDeniedException(permission);
        return actor;
    }

    private async Task SaveAsync(OrderGiftCardDelivery row, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch { db.Entry(row).State = EntityState.Detached; throw; }
    }

    private static void ValidateCard(OrderGiftCardDelivery row, GiftCardIssuedInfo card)
    {
        var purchase = GiftCardDeliveryData.Read(row);
        if (card.Source != GiftCardDeliveryData.Source(row) || card.FaceValue != purchase.FaceValue || card.Currency != purchase.Currency
            || row.GiftCardId is { } existing && existing != card.GiftCardId)
            throw new InvalidStateException("The issued gift card does not match the recorded purchase.");
    }

    private void EnsureUsable(GiftCardIssuedInfo card)
    {
        if (card.ExpiresAtUtc <= clock.UtcNow || card.Status != "Active")
            throw new InvalidStateException("The issued gift card is no longer available for delivery.");
    }

    private bool CanResend(OrderGiftCardDelivery row)
        => row.DeliveryMethod == GiftCardDeliveryMethods.Email && row.GiftCardId is not null
            && row.SendAtUtc <= clock.UtcNow && !(row.ExpiresAtUtc <= clock.UtcNow)
            && (row.LastSentAtUtc is null || row.LastSentAtUtc <= clock.UtcNow.AddMinutes(-5))
            && (row.LastResendRequestedAtUtc is null || row.LastResendRequestedAtUtc <= clock.UtcNow.AddMinutes(-5))
            && (row.ResendWindowStartedAtUtc is null || row.ResendWindowStartedAtUtc <= clock.UtcNow.AddDays(-1) || row.ResendsInWindow < 3);

    private static PhysicalGiftCardDto Physical(OrderGiftCardDelivery row)
    {
        var purchase = GiftCardDeliveryData.Read(row);
        return new(row.Id, row.OrderId, row.DeliveryMethod, purchase.FaceValue, purchase.Currency,
            purchase.RecipientName, row.PostingDate, row.Status, row.CompletedAtUtc, Convert.ToBase64String(row.RowVersion));
    }

    private static string? MaskEmail(string? value)
        => string.IsNullOrEmpty(value) ? null : "***" + (value.Contains('@') ? value[value.LastIndexOf('@')..] : string.Empty);

    private static DateTime? Utc(DateTime? value)
        => value is { } instant ? DateTime.SpecifyKind(instant, DateTimeKind.Utc) : null;

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new InvalidStateException("Choose a valid page and a page size from 1 to 100.");
    }
}
