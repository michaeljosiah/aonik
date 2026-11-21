using System.Net.Http.Headers;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Infrastructure.Integrations.Gmail;

internal sealed class GmailOAuthProvider(System.Net.Http.IHttpClientFactory clients, IHttpMessageHandlerFactory handlers,
    IOptionsMonitor<GmailConnectorOptions> options, IHostEnvironment environment, IClock clock) : IConnectorOAuthProvider
{
    public string ConnectorKey => GmailActionKeys.Connector;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.CurrentValue.ClientId) && !string.IsNullOrWhiteSpace(options.CurrentValue.ClientSecret)
        && ValidRedirect(options.CurrentValue.RedirectUri) && ValidRedirect(options.CurrentValue.ReviewReturnUri);

    public ConnectorAuthorizationChallenge CreateAuthorization(IReadOnlyList<string> scopes, string state)
    {
        using PkceGoogleAuthorizationCodeFlow flow = Flow(scopes);
        var request = (GoogleAuthorizationCodeRequestUrl)flow.CreateAuthorizationCodeRequest(options.CurrentValue.RedirectUri, out string verifier);
        request.State = state;
        request.AccessType = "offline";
        request.Prompt = "consent select_account";
        request.IncludeGrantedScopes = "true";
        return new(request.Build().AbsoluteUri, verifier);
    }

    public async Task<ConnectorOAuthTokens> ExchangeAsync(string code, string verifier, CancellationToken cancellationToken = default)
    {
        using PkceGoogleAuthorizationCodeFlow flow = Flow([]);
        try
        {
            TokenResponse token = await flow.ExchangeCodeForTokenAsync("connector-consent", code, verifier, options.CurrentValue.RedirectUri, cancellationToken);
            return Tokens(token);
        }
        catch (TokenResponseException exception) { throw TokenError(exception); }
    }

    public async Task<ConnectorOAuthTokens> RefreshAsync(string refreshToken, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default)
    {
        using PkceGoogleAuthorizationCodeFlow flow = Flow(scopes);
        try { return Tokens(await flow.RefreshTokenAsync("connector-refresh", refreshToken, cancellationToken)); }
        catch (TokenResponseException exception) { throw TokenError(exception); }
    }

    public async Task<ConnectorAccountIdentity> GetIdentityAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var identity = await GmailHttp.JsonAsync(clients.CreateClient("Gmail"), request, 32_768, cancellationToken);
        if (!identity.TryGetProperty("sub", out var subject) || subject.GetString() is not { Length: > 0 and <= 200 } id
            || !identity.TryGetProperty("email", out var email) || email.GetString() is not { Length: > 0 and <= 254 } label
            || !identity.TryGetProperty("email_verified", out var verified) || verified.ValueKind != System.Text.Json.JsonValueKind.True)
            throw new ConnectorProviderException("ProviderIdentityInvalid");
        return new(id, label);
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/revoke")
        { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token }) };
        using HttpResponseMessage response = await clients.CreateClient("GmailOAuth").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new ConnectorProviderException("ProviderRevocationFailed");
    }

    private PkceGoogleAuthorizationCodeFlow Flow(IReadOnlyList<string> scopes)
    {
        if (!IsConfigured) throw new ConnectorProviderException("ConnectorUnavailable");
        var flow = new PkceGoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = options.CurrentValue.ClientId, ClientSecret = options.CurrentValue.ClientSecret },
            Scopes = scopes, DataStore = null, IncludeGrantedScopes = true,
            DefaultExponentialBackOffPolicy = Google.Apis.Http.ExponentialBackOffPolicy.None,
            HttpClientFactory = new GoogleConnectorHttpClientFactory(handlers),
        });
        flow.HttpClient.Timeout = TimeSpan.FromSeconds(15);
        return flow;
    }
    private bool ValidRedirect(string value) => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Scheme == Uri.UriSchemeHttps || ((environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
    private ConnectorOAuthTokens Tokens(TokenResponse token)
    {
        if (string.IsNullOrWhiteSpace(token.AccessToken) || token.ExpiresInSeconds is not (> 0 and <= 86_400))
            throw new ConnectorProviderException("ProviderTokenInvalid");
        return new(token.AccessToken, token.RefreshToken, clock.UtcNow.AddSeconds(token.ExpiresInSeconds.Value),
            string.IsNullOrWhiteSpace(token.Scope) ? null : token.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(scope => scope == "https://www.googleapis.com/auth/userinfo.email" ? "email" : scope).Distinct(StringComparer.Ordinal).ToList());
    }
    private static ConnectorProviderException TokenError(TokenResponseException exception) => new(
        exception.Error?.Error == "invalid_grant" ? "ReauthorizationRequired" : "ProviderAuthorizationFailed");
}
