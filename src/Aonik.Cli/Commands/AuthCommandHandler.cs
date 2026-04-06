using Aonik.Cli.Abstractions;
using Aonik.Cli.Models;

namespace Aonik.Cli.Commands;

public sealed class AuthCommandHandler
{
    private readonly IAonikCliApiClient _apiClient;
    private readonly ISessionStore _sessionStore;
    private readonly ICliOutputWriter _outputWriter;

    public AuthCommandHandler(
        IAonikCliApiClient apiClient,
        ISessionStore sessionStore,
        ICliOutputWriter outputWriter)
    {
        _apiClient = apiClient;
        _sessionStore = sessionStore;
        _outputWriter = outputWriter;
    }

    public async Task<int> LoginAsync(LoginOptions options, CancellationToken cancellationToken = default)
    {
        ValidateLoginOptions(options);

        PublicAuthProviderSettingsResponse? settings = null;
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            settings = await _apiClient.GetPublicAuthProviderSettingsAsync(options.BaseUrl, cancellationToken);
        }

        CliSession session;
        if (!string.IsNullOrWhiteSpace(options.AccessToken))
        {
            session = new CliSession(
                options.BaseUrl,
                options.AccessToken,
                RefreshToken: null,
                ExpiresAt: null,
                ActiveProvider: settings?.ActiveProvider,
                TenantId: options.TenantId,
                UserId: null,
                Email: null,
                LastSessionId: null,
                LastThreadId: null);
        }
        else
        {
            var tokenResponse = await _apiClient.ExchangeTokenAsync(
                options.BaseUrl,
                new TokenRequestDto(
                    GrantType: "password",
                    ClientId: ResolveClientId(options, settings),
                    Username: options.Username,
                    Password: options.Password,
                    Scope: options.Scope,
                    RedirectUri: null,
                    CodeVerifier: null,
                    AuthorizationCode: null,
                    RefreshToken: null),
                cancellationToken);

            session = new CliSession(
                options.BaseUrl,
                tokenResponse.AccessToken,
                tokenResponse.RefreshToken,
                DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                settings?.ActiveProvider,
                options.TenantId,
                UserId: null,
                Email: null,
                LastSessionId: null,
                LastThreadId: null);
        }

        var userInfo = await _apiClient.GetUserInfoAsync(session, cancellationToken);
        session = session with
        {
            UserId = userInfo.UserId,
            TenantId = userInfo.TenantId,
            Email = userInfo.Email
        };

        await _sessionStore.SaveAsync(session, cancellationToken);
        await _outputWriter.WriteSessionAsync(session, options.OutputMode, cancellationToken);
        return 0;
    }

    public async Task<int> StatusAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        await _outputWriter.WriteSessionAsync(session, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> WhoAmIAsync(OutputMode outputMode, CancellationToken cancellationToken = default)
    {
        var session = await RequireSessionAsync(cancellationToken);
        var userInfo = await _apiClient.GetUserInfoAsync(session, cancellationToken);

        var updatedSession = session with
        {
            UserId = userInfo.UserId,
            TenantId = userInfo.TenantId,
            Email = userInfo.Email
        };

        await _sessionStore.SaveAsync(updatedSession, cancellationToken);
        await _outputWriter.WriteUserInfoAsync(userInfo, outputMode, cancellationToken);
        return 0;
    }

    public async Task<int> LogoutAsync(CancellationToken cancellationToken = default)
    {
        await _sessionStore.ClearAsync(cancellationToken);
        await _outputWriter.WriteInfoAsync("AONIK CLI session cleared.", cancellationToken);
        return 0;
    }

    private async Task<CliSession> RequireSessionAsync(CancellationToken cancellationToken)
    {
        var session = await _sessionStore.LoadAsync(cancellationToken);
        if (session is null)
        {
            throw new AonikCliException("No active session found. Run 'aonik auth login' first.");
        }

        return session;
    }

    private static void ValidateLoginOptions(LoginOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new AonikCliException("'--base-url' is required.");
        }

        if (!string.IsNullOrWhiteSpace(options.AccessToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password))
        {
            throw new AonikCliException("Provide either '--access-token' or both '--username' and '--password'.");
        }
    }

    private static string ResolveClientId(LoginOptions options, PublicAuthProviderSettingsResponse? settings)
    {
        if (!string.IsNullOrWhiteSpace(options.ClientId))
        {
            return options.ClientId;
        }

        if (settings is null)
        {
            return string.Empty;
        }

        return string.Equals(settings.ActiveProvider, "Auth0", StringComparison.OrdinalIgnoreCase)
            ? settings.Auth0.ClientId ?? string.Empty
            : settings.AzureAd.ClientId ?? string.Empty;
    }
}
