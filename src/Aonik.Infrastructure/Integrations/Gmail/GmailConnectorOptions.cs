namespace Aonik.Infrastructure.Integrations.Gmail;

internal sealed class GmailConnectorOptions
{
    public const string Section = "Integrations:Gmail:OAuth";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string ReviewReturnUri { get; set; } = string.Empty;
}
