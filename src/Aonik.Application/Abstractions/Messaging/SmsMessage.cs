namespace Aonik.Application.Abstractions.Messaging;

public record SmsMessage(
    string To,
    string Body,
    string? From = null);
