using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Platform.Contracts.Models.ContactEnquiries;
using Aonik.Platform.Contracts.Services.ContactEnquiries;
using Aonik.Platform.Entities.ContactEnquiries;
using Aonik.Platform.Persistence;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Messaging;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.SharedKernel.Abstractions.Storage;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Platform.Services.ContactEnquiries;

internal sealed class ContactEnquiryService(
    PlatformDbContext db,
    ITenantProvider tenantProvider,
    IClock clock,
    ITenantSettingStore settings,
    IContactImageProcessor processor,
    [FromKeyedServices(FileStoreKeys.ContactImages)] IFileStore files,
    ITemplatedEmailSender emailSender,
    ICurrentUserProvider currentUserProvider,
    IPermissionService permissions) : AdminServiceBase(currentUserProvider, permissions), IContactEnquiryService
{
    private const int MaxImageBytes = 10 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };

    public async Task<ContactEnquiryReceipt> SubmitAsync(ContactEnquirySubmission command, CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(command);
        var tenantId = tenantProvider.GetCurrentTenantId();
        if (tenantId == Guid.Empty) throw new ContactEnquiryUnavailableException();
        var hash = SubmissionHash(normalized);
        var existing = await FindSubmissionAsync(tenantId, normalized.SubmissionId, cancellationToken);
        if (existing is not null) return Replay(existing, hash);

        var configuration = await ConfigurationAsync(tenantId, normalized.Topic, cancellationToken);
        var processed = new List<ProcessedContactImage>();
        var problems = new List<ContactImageProblem>();
        for (var i = 0; i < normalized.Images.Count; i++)
        {
            try
            {
                var image = await processor.ProcessAsync(normalized.Images[i], cancellationToken);
                if (image.Content.Length is 0 or > MaxImageBytes || image.ContentType is not ("image/jpeg" or "image/png")
                    || SafeFileName(image.FileName) != image.FileName)
                    throw new ContactEnquiryUnavailableException();
                processed.Add(image);
            }
            catch (ContactImageValidationException exception)
            {
                problems.Add(new(i, normalized.Images[i].FileName, exception.Code, exception.Message));
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                throw new ContactEnquiryUnavailableException();
            }
        }
        if (problems.Count > 0) throw new ContactEnquiryValidationException(new Dictionary<string, string[]>(), problems);

        var enquiry = new ContactEnquiry
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SubmissionId = normalized.SubmissionId, SubmissionHash = hash,
            Name = normalized.Name, Email = normalized.Email, Topic = normalized.Topic, OrderNumber = normalized.OrderNumber,
            Message = normalized.Message, ReceivedAtUtc = clock.UtcNow,
            StaffRecipientEmail = Email(configuration.TopicRecipients[normalized.Topic])!,
        };
        enquiry.StaffDetailUrl = configuration.AdminOrigin.TrimEnd('/') + "/contact-enquiries/" + enquiry.Id.ToString("D");
        var uploadedKeys = new List<string>();
        try
        {
            foreach (var image in processed)
            {
                using var content = new MemoryStream(image.Content, writable: false);
                var uploaded = await files.UploadAsync(tenantId, enquiry.Id, content, image.FileName, image.ContentType, cancellationToken);
                uploadedKeys.Add(uploaded.StorageKey);
                enquiry.Images.Add(new ContactEnquiryAttachment
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, EnquiryId = enquiry.Id,
                    FileName = image.FileName, ContentType = image.ContentType, SizeBytes = image.Content.LongLength,
                    StorageKey = uploaded.StorageKey, Sha256 = Convert.ToHexStringLower(SHA256.HashData(image.Content))
                });
            }
        }
        catch (Exception exception)
        {
            await DeleteUploadedAsync(uploadedKeys);
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
            throw new ContactEnquiryUnavailableException();
        }

        var previousOutbox = db.ChangeTracker.Entries<OutboxMessage>().Select(x => x.Entity)
            .ToHashSet(ReferenceEqualityComparer.Instance);
        db.ContactEnquiries.Add(enquiry);
        db.EnqueueIntegrationEvent(new ContactEnquiryDeliveryRequestedEvent(tenantId, enquiry.Id, "staff"));
        db.EnqueueIntegrationEvent(new ContactEnquiryDeliveryRequestedEvent(tenantId, enquiry.Id, "acknowledgement"));
        var addedOutbox = db.ChangeTracker.Entries<OutboxMessage>().Select(x => x.Entity)
            .Where(x => !previousOutbox.Contains(x)).ToList();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Receipt(enquiry);
        }
        catch (Exception exception)
        {
            // A response failure can follow a successful commit. Detach only this attempt, then
            // prove what persisted before deciding whether its private objects may be removed.
            foreach (var attachment in enquiry.Images.ToArray()) db.Entry(attachment).State = EntityState.Detached;
            db.Entry(enquiry).State = EntityState.Detached;
            foreach (var message in addedOutbox) db.Entry(message).State = EntityState.Detached;
            ContactEnquiry? winner;
            using var verificationTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                winner = await FindSubmissionAsync(tenantId, normalized.SubmissionId, verificationTimeout.Token);
                if (winner?.Id != enquiry.Id)
                {
                    var referenced = await db.ContactEnquiryAttachments.AsNoTracking()
                        .Where(x => x.TenantId == tenantId && uploadedKeys.Contains(x.StorageKey))
                        .Select(x => x.StorageKey).ToListAsync(verificationTimeout.Token);
                    await DeleteUploadedAsync(uploadedKeys.Except(referenced));
                }
            }
            catch
            {
                // Unknown commit outcome: retain private objects for storage lifecycle/reconciliation.
                if (exception is OperationCanceledException) cancellationToken.ThrowIfCancellationRequested();
                throw new ContactEnquiryUnavailableException();
            }
            if (winner is not null) return Replay(winner, hash);
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
            throw new ContactEnquiryUnavailableException();
        }
    }

    public async Task<Aonik.SharedKernel.Abstractions.PagedResult<ContactEnquirySummaryDto>> ListAsync(
        int page = 1, int pageSize = 20, string? topic = null, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ContactEnquiryValidationException(new Dictionary<string, string[]> { ["page"] = ["Choose a valid page and page size (1–100)."] });
        if (topic is not null && !ContactEnquiryTopics.IsKnown(topic))
            throw new ContactEnquiryValidationException(new Dictionary<string, string[]> { ["topic"] = ["Choose a listed topic."] });
        var tenantId = tenantProvider.GetCurrentTenantId();
        var query = db.ContactEnquiries.AsNoTracking().Where(x => x.TenantId == tenantId);
        if (topic is not null) query = query.Where(x => x.Topic == topic);
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.ReceivedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ContactEnquirySummaryDto(x.Id, x.Name, x.Email, x.Topic, x.ReceivedAtUtc,
                x.Images.Count(image => image.TenantId == tenantId)))
            .ToListAsync(cancellationToken);
        return new(rows, count, page, pageSize);
    }

    public async Task<ContactEnquiryDetailDto?> GetAsync(Guid enquiryId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);
        var tenantId = tenantProvider.GetCurrentTenantId();
        var enquiry = await db.ContactEnquiries.AsNoTracking().Include(x => x.Images)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == enquiryId, cancellationToken);
        return enquiry is null ? null : new(enquiry.Id, enquiry.Name, enquiry.Email, enquiry.Topic, enquiry.OrderNumber,
            enquiry.Message, enquiry.ReceivedAtUtc, enquiry.Images.Where(x => x.TenantId == tenantId).OrderBy(x => x.Id)
                .Select(x => new ContactEnquiryAttachmentDto(x.Id, x.FileName, x.ContentType, x.SizeBytes)).ToList());
    }

    public async Task<ContactEnquiryImageDownload?> OpenImageAsync(Guid enquiryId, Guid imageId, CancellationToken cancellationToken = default)
    {
        await EnsurePermissionAsync("Customers.Read", cancellationToken);
        var tenantId = tenantProvider.GetCurrentTenantId();
        if (!await db.ContactEnquiries.AnyAsync(x => x.TenantId == tenantId && x.Id == enquiryId, cancellationToken)) return null;
        var image = await db.ContactEnquiryAttachments.AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.EnquiryId == enquiryId && x.Id == imageId, cancellationToken);
        if (image is null) return null;
        try
        {
            var stream = await files.OpenReadAsync(image.StorageKey, cancellationToken);
            return stream is null ? null : new(stream, image.ContentType, image.FileName);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new ContactEnquiryUnavailableException();
        }
    }

    public async Task DeliverAsync(Guid enquiryId, string audience, CancellationToken cancellationToken = default)
    {
        if (audience is not ("staff" or "acknowledgement")) throw new InvalidStateException("Unknown enquiry email audience.");
        var tenantId = tenantProvider.GetCurrentTenantId();
        var enquiry = await db.ContactEnquiries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == enquiryId, cancellationToken);
        if (enquiry is null) return;
        var model = new Dictionary<string, object?>
        {
            ["enquiry_id"] = enquiry.Id.ToString("D"), ["received_at"] = enquiry.ReceivedAtUtc.ToString("u")
        };
        if (audience == "staff")
        {
            model["name"] = enquiry.Name;
            model["email"] = enquiry.Email;
            model["topic"] = enquiry.Topic;
            model["order_number"] = enquiry.OrderNumber;
            model["message"] = enquiry.Message;
            model["detail_url"] = enquiry.StaffDetailUrl;
        }
        await emailSender.SendAsync(new(audience == "staff" ? ContactEnquiryEmailTemplates.Staff : ContactEnquiryEmailTemplates.Acknowledgement,
            audience == "staff" ? enquiry.StaffRecipientEmail : enquiry.Email, model), cancellationToken);
    }

    private Task<ContactEnquiry?> FindSubmissionAsync(Guid tenantId, Guid submissionId, CancellationToken cancellationToken)
        => db.ContactEnquiries.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.SubmissionId == submissionId, cancellationToken);

    private static ContactEnquiryReceipt Receipt(ContactEnquiry enquiry) => new(enquiry.Id, enquiry.ReceivedAtUtc);
    private static ContactEnquiryReceipt Replay(ContactEnquiry enquiry, string hash)
        => enquiry.SubmissionHash == hash ? Receipt(enquiry) : throw new ContactEnquiryConflictException();

    private async Task DeleteUploadedAsync(IEnumerable<string> keys)
    {
        using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        foreach (var key in keys)
        {
            try { await files.DeleteAsync(key, cleanupTimeout.Token); }
            catch { /* Private storage lifecycle handles unreachable orphan objects. */ }
        }
    }

    private async Task<ContactEnquiriesConfiguration> ConfigurationAsync(Guid tenantId, string topic, CancellationToken cancellationToken)
    {
        var json = await settings.GetTenantValueAsync(ContactEnquirySettingNames.Configuration, tenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(json) || json.Length > 16000) throw new ContactEnquiryUnavailableException();
        ContactEnquiriesConfiguration? configuration;
        try { configuration = JsonSerializer.Deserialize<ContactEnquiriesConfiguration>(json, JsonOptions); }
        catch (JsonException) { throw new ContactEnquiryUnavailableException(); }
        if (configuration is not { IsEnabled: true } || configuration.TopicRecipients is null
            || !configuration.TopicRecipients.TryGetValue(topic, out var recipient) || Email(recipient) is null
            || !Uri.TryCreate(configuration.AdminOrigin, UriKind.Absolute, out var origin)
            || origin.Scheme != Uri.UriSchemeHttps || origin.UserInfo.Length > 0 || origin.AbsolutePath != "/"
            || origin.Query.Length > 0 || origin.Fragment.Length > 0 || origin.AbsoluteUri.Length > 1900)
            throw new ContactEnquiryUnavailableException();
        return configuration with { AdminOrigin = origin.GetLeftPart(UriPartial.Authority) };
    }

    private static ContactEnquirySubmission Normalize(ContactEnquirySubmission command)
    {
        var errors = new Dictionary<string, string[]>();
        var name = command.Name?.Trim() ?? string.Empty;
        var email = Email(command.Email);
        var topic = command.Topic?.Trim() ?? string.Empty;
        var message = command.Message?.Trim() ?? string.Empty;
        var orderNumber = topic == "order" ? command.OrderNumber?.Trim() : null;
        if (string.IsNullOrEmpty(orderNumber)) orderNumber = null;
        if (command.SubmissionId == Guid.Empty) errors["submission_id"] = ["Supply a submission ID for safe retries."];
        if (name.Length is < 1 or > 200 || name.Any(char.IsControl)) errors["name"] = ["Enter a name of up to 200 characters."];
        if (email is null) errors["email"] = ["Enter a valid email address of up to 254 characters."];
        if (!ContactEnquiryTopics.IsKnown(topic)) errors["topic"] = ["Choose a listed topic."];
        if (orderNumber?.Length > 64 || orderNumber?.Any(char.IsControl) == true) errors["order_number"] = ["Use an order number of up to 64 characters."];
        if (message.Length is < 10 or > 5000 || message.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            errors["message"] = ["Enter a message of 10 to 5,000 characters."];
        var images = command.Images ?? [];
        if (images.Count > 3) errors["images"] = ["Attach no more than three images."];
        var problems = new List<ContactImageProblem>();
        var normalizedImages = new List<ContactImageUpload>();
        for (var i = 0; i < Math.Min(images.Count, 3); i++)
        {
            var image = images[i];
            var fileName = SafeFileName(image.FileName);
            if (fileName is null || image.Content is null || image.Content.Length is 0 or > MaxImageBytes
                || string.IsNullOrWhiteSpace(image.ContentType) || image.ContentType.Length > 100)
                problems.Add(new(i, fileName ?? "image", "invalid_file", "Choose an image no larger than 10 MiB with a valid file name."));
            else normalizedImages.Add(image with { FileName = fileName, ContentType = image.ContentType.Trim().ToLowerInvariant() });
        }
        if (errors.Count > 0 || problems.Count > 0) throw new ContactEnquiryValidationException(errors, problems);
        return new(command.SubmissionId, name, email!, topic, orderNumber, message, normalizedImages);
    }

    private static string? Email(string? value)
    {
        value = value?.Trim();
        return value is { Length: > 0 and <= 254 } && !value.Any(char.IsControl)
            && MailAddress.TryCreate(value, out var address) && address.Address == value && address.DisplayName.Length == 0
            ? value.ToLowerInvariant() : null;
    }

    private static string? SafeFileName(string? value)
    {
        var name = value?.Replace('\\', '/').Split('/').Last().Trim();
        return name is { Length: > 0 and <= 200 } && name is not ("." or "..")
            && !name.Any(c => char.IsControl(c) || c is '"' or '<' or '>' or ':') ? name : null;
    }

    private static string SubmissionHash(ContactEnquirySubmission command)
        => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            command.Name, command.Email, command.Topic, command.OrderNumber, command.Message,
            Images = command.Images.Select(x => new { x.FileName, x.ContentType, Hash = Convert.ToHexStringLower(SHA256.HashData(x.Content)) })
        }, JsonOptions)));
}
