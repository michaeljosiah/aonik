using System.Text.Json;

namespace Aonik.SharedKernel.Abstractions.Connectors;

public interface IConnectorActionProvider
{
    string ConnectorKey { get; }
    void ValidateArguments(ConnectorActionRef action, JsonElement arguments);
    Task<JsonElement> InvokeAsync(ConnectorProviderCall call, CancellationToken cancellationToken = default);
}

/// <summary>Credentials cross only the reviewed in-process provider boundary and must never be serialized into tools or audit.</summary>
public sealed record ConnectorProviderCall(Guid ConnectionId, string ProviderAccountId, string AccessToken,
    ConnectorActionRef Action, JsonElement Arguments, Guid OperationId)
{
    public override string ToString() => $"ConnectorProviderCall({ConnectionId:D}, {Action.Key}@{Action.Version})";
}

public interface IConnectorOAuthProvider
{
    string ConnectorKey { get; }
    bool IsConfigured { get; }
    ConnectorAuthorizationChallenge CreateAuthorization(IReadOnlyList<string> scopes, string state);
    Task<ConnectorOAuthTokens> ExchangeAsync(string code, string verifier, CancellationToken cancellationToken = default);
    Task<ConnectorOAuthTokens> RefreshAsync(string refreshToken, IReadOnlyList<string> scopes, CancellationToken cancellationToken = default);
    Task<ConnectorAccountIdentity> GetIdentityAsync(string accessToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(string token, CancellationToken cancellationToken = default);
}
public sealed record ConnectorAuthorizationChallenge(string AuthorizationUrl, string Verifier)
{
    public override string ToString() => "ConnectorAuthorizationChallenge(<private>)";
}
public sealed record ConnectorOAuthTokens(string AccessToken, string? RefreshToken, DateTime ExpiresAt, IReadOnlyList<string>? GrantedScopes)
{
    public override string ToString() => "ConnectorOAuthTokens(<private>)";
}
public sealed record ConnectorAccountIdentity(string Id, string Label);
public sealed class ConnectorAccessException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
public sealed class ConnectorProviderException(string code, bool transient = false, TimeSpan? retryAfter = null) : Exception(code)
{
    public string Code { get; } = code;
    public bool Transient { get; } = transient;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
