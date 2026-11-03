namespace Aonik.Platform.Contracts.Services.Messaging;

public record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsHtml = true,
    string? From = null,
    IReadOnlyList<Aonik.SharedKernel.Abstractions.Messaging.EmailAttachment>? Attachments = null,
    Guid? TrackingId = null);
