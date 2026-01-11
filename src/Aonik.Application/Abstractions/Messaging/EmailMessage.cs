namespace Aonik.Application.Abstractions.Messaging;

public record EmailMessage(
    string To,
    string Subject,
    string Body,
    bool IsHtml = true,
    string? From = null);
