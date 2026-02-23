namespace Aonik.Platform.Contracts.Services.Messaging;

public record SmsMessage(
    string To,
    string Body,
    string? From = null);
