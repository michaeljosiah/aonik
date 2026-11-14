namespace Aonik.Infrastructure.Integrations.Gmail;

internal sealed class GmailMailboxOptions
{
    public const string Section = "Integrations:Gmail:Mailbox";
    public string PushTopic { get; set; } = string.Empty;
    public string PushSubscription { get; set; } = string.Empty;
    public string PushAudience { get; set; } = string.Empty;
    public string PushServiceAccount { get; set; } = string.Empty;
}
