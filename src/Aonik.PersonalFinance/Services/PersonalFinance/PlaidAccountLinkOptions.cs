namespace Aonik.PersonalFinance.Services;

internal sealed class PlaidAccountLinkOptions
{
    public bool UseRealPlaidApi { get; set; }

    public string BaseUrl { get; set; } = "https://sandbox.plaid.com";

    public string ClientId { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public string ClientName { get; set; } = "Payabo";

    public string Language { get; set; } = "en";

    public List<string> Products { get; set; } = ["transactions"];

    public List<string> CountryCodes { get; set; } = ["GB"];

    public string? LinkCustomizationName { get; set; }

    public string? RedirectUri { get; set; }

    public string? WebhookUrl { get; set; }

    public bool IsConfigured()
    {
        return UseRealPlaidApi
            && !string.IsNullOrWhiteSpace(BaseUrl)
            && !string.IsNullOrWhiteSpace(ClientId)
            && !string.IsNullOrWhiteSpace(Secret);
    }
}
