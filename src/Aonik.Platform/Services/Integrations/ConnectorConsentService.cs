using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

internal sealed record ConnectorConsentChallenge(Guid RequestId, string AuthorizationUrl, DateTime ExpiresAt,
    IReadOnlyList<string> Scopes, [property: JsonIgnore] string Cookie);
internal sealed record ConnectorConsentProof(Guid TenantId, Guid RequestId, Guid UserId, string StateHash, DateTime ExpiresAt);

/// <summary>Provider consent is staged privately; only the authenticated consenting owner may apply the account and grants.</summary>
internal sealed class ConnectorConsentService(PlatformDbContext database, ConnectorAuthorization authorization,
    ConnectorConnectionRequestService requests, ConnectorGrantService grants, ConnectorCredentialProtector credentials,
    IEnumerable<IConnectorOAuthProvider> providers, IDataProtectionProvider protection, IAuditLogWriter audit, IClock clock)
{
    private IDataProtector CookieProtector => protection.CreateProtector("Aonik.ConnectorConsentCookie.v1");

    public async Task<ConnectorConsentChallenge> BeginAsync(Guid requestId, ConnectionRequestCommand command, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        ConnectorConnectionRequest request = await FindAsync(requestId, cancellationToken);
        await RequireOwnerAsync(request, userId, cancellationToken);
        ConnectorGrantService.Version(request.RowVersion, command.ExpectedVersion);
        requests.RequireActive(request);
        if (request.Status != "AwaitingConsent") throw Conflict("The request has not reached account-owner consent.");
        await requests.RequireConsentPolicyAsync(request, cancellationToken);
        IConnectorOAuthProvider provider = Provider(request.ConnectorKey);
        await audit.LogAsync("Connector.ConsentRequested", "ConnectorConnectionRequest", request.Id, request.TenantId, userId, null, null, cancellationToken);
        ConnectorConnection connection;
        if (request.ConnectionId is { } connectionId)
            connection = await database.ConnectorConnections.SingleAsync(connection => connection.TenantId == request.TenantId && connection.Id == connectionId, cancellationToken);
        else
        {
            connection = new() { TenantId = request.TenantId, ConnectorKey = request.ConnectorKey, Ownership = request.Ownership,
                OwnerUserId = request.OwnerUserId, ProvisionedByUserId = userId, DisplayName = "Gmail account awaiting consent" };
            database.ConnectorConnections.Add(connection);
            request.ConnectionId = connection.Id;
        }
        IReadOnlyList<string> scopes = ConnectorJson.Read<List<string>>(request.RequiredScopesJson)
            .Union(ConnectorJson.Read<List<string>>(connection.GrantedScopesJson), StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        string state = $"{request.Id:N}.{WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32))}";
        DateTime expires = clock.UtcNow.AddMinutes(10);
        if (expires > request.ExpiresAt) expires = request.ExpiresAt;
        ConnectorAuthorizationChallenge challenge = provider.CreateAuthorization(scopes, state);
        ConnectorConnectionRequestService.ClearConsent(request);
        request.OAuthStateHash = Hash(state);
        request.ProtectedVerifier = credentials.ProtectRequest(request.TenantId, request.Id, userId, "Verifier", challenge.Verifier);
        request.OAuthStartedByUserId = userId; request.OAuthExpiresAt = expires;
        request.ConsentedAt = null; request.ConsentedAccountId = null; request.ConsentedAccountLabel = null;
        requests.StageWake(request, "ConsentExpire", expires);
        await SaveAsync(cancellationToken);
        string cookie = CookieProtector.Protect(ConnectorJson.Serialize(new ConnectorConsentProof(request.TenantId, request.Id, userId, Hash(state), expires)));
        return new(request.Id, challenge.AuthorizationUrl, expires, scopes, cookie);
    }

    internal ConnectorConsentProof ReadProof(string cookie, string state)
    {
        try
        {
            if (cookie.Length > 8192 || state.Length > 256) throw new ArgumentException("Invalid consent proof.");
            ConnectorConsentProof proof = ConnectorJson.Read<ConnectorConsentProof>(CookieProtector.Unprotect(cookie));
            if (proof.ExpiresAt <= clock.UtcNow || proof.RequestId == Guid.Empty || proof.TenantId == Guid.Empty || proof.UserId == Guid.Empty
                || proof.StateHash != Hash(state) || !state.StartsWith(proof.RequestId.ToString("N") + ".", StringComparison.Ordinal))
                throw new ArgumentException("Invalid consent proof.");
            return proof;
        }
        catch (Exception exception) when (exception is CryptographicException or System.Text.Json.JsonException or ArgumentException)
        { throw new ConnectorAccessException("ConsentInvalid", "This account consent session is invalid or expired."); }
    }

    public async Task<Guid> ReceiveCallbackAsync(string cookie, string state, string code, Guid? authenticatedUserId,
        CancellationToken cancellationToken = default)
    {
        ConnectorConsentProof proof = ReadProof(cookie, state);
        if (proof.TenantId != authorization.TenantId || (authenticatedUserId.HasValue && authenticatedUserId != proof.UserId)
            || string.IsNullOrWhiteSpace(code) || code.Length > 4096) throw new PermissionDeniedException("Integration.Use");
        await authorization.RequireActorAsync(new("User", proof.UserId, proof.UserId), "Integration.Read", cancellationToken);
        ConnectorConnectionRequest request = await FindAsync(proof.RequestId, cancellationToken);
        await RequireOwnerAsync(request, proof.UserId, cancellationToken);
        requests.RequireActive(request);
        if (request.Status != "AwaitingConsent" || request.OAuthStateHash != proof.StateHash || request.OAuthStartedByUserId != proof.UserId
            || request.OAuthExpiresAt is null || request.OAuthExpiresAt <= clock.UtcNow || request.ProtectedVerifier is null || request.OAuthLeaseExpiresAt > clock.UtcNow)
            throw Conflict("This consent response is stale or has already been used.");
        await requests.RequireConsentPolicyAsync(request, cancellationToken);
        Guid lease = Guid.NewGuid();
        string verifier = ReadSecret(request, proof.UserId, "Verifier", request.ProtectedVerifier);
        request.OAuthLeaseId = lease; request.OAuthLeaseExpiresAt = clock.UtcNow.AddMinutes(1);
        await SaveAsync(cancellationToken);
        try
        {
            IConnectorOAuthProvider provider = Provider(request.ConnectorKey);
            ConnectorOAuthTokens tokens = await provider.ExchangeAsync(code, verifier, cancellationToken);
            ConnectorAccountIdentity account = await provider.GetIdentityAsync(tokens.AccessToken, cancellationToken);
            await database.Entry(request).ReloadAsync(cancellationToken);
            if (request.Status != "AwaitingConsent" || request.OAuthLeaseId != lease || request.OAuthStateHash != proof.StateHash
                || request.ExpiresAt <= clock.UtcNow || request.OAuthExpiresAt is null || request.OAuthExpiresAt <= clock.UtcNow) throw Conflict("The request changed during account consent.");
            ConnectorConnection connection = await database.ConnectorConnections.AsNoTracking().SingleAsync(connection => connection.TenantId == request.TenantId
                && connection.Id == request.ConnectionId, cancellationToken);
            if (connection.ProviderAccountId.Length > 0 && connection.ProviderAccountId != account.Id)
                throw new ConnectorProviderException("ProviderAccountChanged");
            request.ProtectedOAuthResult = credentials.ProtectRequest(request.TenantId, request.Id, proof.UserId, "Result", ConnectorJson.Serialize(new ConsentResult(tokens, account)));
            request.ConsentedAccountId = account.Id; request.ConsentedAccountLabel = account.Label; request.ConsentedAt = clock.UtcNow;
            request.OAuthStateHash = null; request.ProtectedVerifier = null; request.OAuthLeaseId = null; request.OAuthLeaseExpiresAt = null;
            request.ReasonCode = "AccountConfirmationRequired";
            request.NextAction = "Sign in to AONIK as the consenting owner and confirm this exact account and requested use.";
            await SaveAsync(cancellationToken);
            return request.Id;
        }
        catch (Exception exception) when (exception is ConnectorProviderException or OperationCanceledException or HttpRequestException)
        {
            await database.Entry(request).ReloadAsync(CancellationToken.None);
            if (request.OAuthLeaseId == lease)
            {
                ConnectorConnectionRequestService.ClearConsent(request);
                request.ReasonCode = exception is ConnectorProviderException provider ? provider.Code : "ConsentExchangeUncertain";
                request.NextAction = "Restart owner consent for this unchanged request. The callback did not grant account access.";
                try { await database.SaveChangesAsync(CancellationToken.None); }
                catch (DbUpdateConcurrencyException) { }
            }
            throw;
        }
    }

    public async Task<ConnectionRequestDetail> ConfirmAsync(Guid requestId, ConnectionRequestCommand command, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        ConnectorConnectionRequest request = await FindAsync(requestId, cancellationToken);
        await RequireOwnerAsync(request, userId, cancellationToken);
        if (request.Status == "Fulfilled") return await requests.DetailAsync(request, userId, cancellationToken);
        ConnectorGrantService.Version(request.RowVersion, command.ExpectedVersion);
        requests.RequireActive(request);
        if (request.Status != "AwaitingConsent" || request.OAuthStartedByUserId != userId || request.ProtectedOAuthResult is null
            || request.ConsentedAt is null || request.OAuthExpiresAt is null || request.OAuthExpiresAt <= clock.UtcNow) throw Conflict("A current provider consent result is required.");
        await requests.RequireConsentPolicyAsync(request, cancellationToken);
        ConsentResult result = ConnectorJson.Read<ConsentResult>(ReadSecret(request, userId, "Result", request.ProtectedOAuthResult));
        ConnectorOAuthTokens tokens = result.Tokens;
        IReadOnlyList<string> required = ConnectorJson.Read<List<string>>(request.RequiredScopesJson);
        if (tokens.GrantedScopes is null || required.Any(scope => !tokens.GrantedScopes.Contains(scope, StringComparer.Ordinal)))
            throw ConnectorAuthorization.Access("InsufficientScopes");
        if (tokens.ExpiresAt <= clock.UtcNow.AddSeconds(15)) throw ConnectorAuthorization.Access("ReauthorizationRequired");
        ConnectorConnection connection = await database.ConnectorConnections.SingleAsync(connection => connection.TenantId == request.TenantId
            && connection.Id == request.ConnectionId, cancellationToken);
        await grants.RequireManagerAsync(connection, userId, cancellationToken);
        if (connection.ProviderAccountId.Length > 0 && connection.ProviderAccountId != result.Account.Id)
            throw new ConnectorAccessException("ProviderAccountChanged", "Reconnect must use the same provider account.");
        if (await database.ConnectorConnections.AsNoTracking().AnyAsync(other => other.TenantId == request.TenantId && other.Id != connection.Id
            && other.ConnectorKey == request.ConnectorKey && other.Ownership == request.Ownership && other.OwnerUserId == request.OwnerUserId
            && other.ProviderAccountId == result.Account.Id && other.Status != "Revoked", cancellationToken))
            throw new ConnectorAccessException("AccountAlreadyConnected", "Select the existing account in a new exact-use request.");
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken) && connection.ProviderAccountId == result.Account.Id && connection.ProtectedCredentials.Length > 0)
            tokens = tokens with { RefreshToken = credentials.Read(connection).RefreshToken };
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken)) throw new ConnectorAccessException("OfflineConsentRequired", "Offline refresh consent is required for this connection.");
        await audit.LogAsync("Connector.ConsentApplicationRequested", "ConnectorConnectionRequest", request.Id, request.TenantId, userId, null,
            ConnectorJson.Serialize(new { request.ConnectionId, request.RequestHash, request.ActionsJson, requiredScopes = required }), cancellationToken);
        connection.ProviderAccountId = result.Account.Id; connection.ProviderAccountLabel = result.Account.Label;
        if (connection.CredentialVersion == 0) connection.DisplayName = result.Account.Label[..Math.Min(result.Account.Label.Length, 160)];
        connection.CredentialVersion++; connection.ProtectedCredentials = credentials.Protect(connection, tokens);
        connection.TokenExpiresAt = tokens.ExpiresAt; connection.GrantedScopesJson = ConnectorJson.Serialize(tokens.GrantedScopes);
        connection.Status = "Active"; connection.LastHealthyAt = clock.UtcNow; connection.LastErrorCode = null;
        connection.RefreshLeaseId = null; connection.RefreshLeaseExpiresAt = null;
        (Guid userGrant, Guid? workflowGrant, Guid? automationGrant) = await grants.StageConsentGrantsAsync(connection, request, userId, cancellationToken);
        request.GrantId = userGrant; request.WorkflowGrantId = workflowGrant; request.Status = "Fulfilled"; request.ActiveRequestHash = null;
        request.AutomationGrantId = automationGrant;
        request.FulfilledAt = clock.UtcNow; request.FulfilledByUserId = userId;
        request.ReasonCode = "Ready"; request.NextAction = "Access is ready. Bind/publish or explicitly resume the unchanged workflow after review.";
        ConnectorConnectionRequestService.ClearConsent(request);
        requests.StageWake(request, "Notify", clock.UtcNow, request.RequesterUserId);
        await SaveAsync(cancellationToken);
        return await requests.DetailAsync(request, userId, cancellationToken);
    }

    internal async Task RequireOwnerAsync(ConnectorConnectionRequest request, Guid userId, CancellationToken cancellationToken)
    {
        if (request.TenantId != authorization.TenantId || (request.Ownership == "User" ? request.OwnerUserId != userId
            : !await requests.CanReviewAsync(request, userId, cancellationToken))) throw new PermissionDeniedException("Integration.Use");
        await authorization.RequireActorAsync(new("User", userId, userId), request.Ownership == "User" ? "Integration.Use" : "Integration.Manage", cancellationToken);
    }
    private string ReadSecret(ConnectorConnectionRequest request, Guid userId, string purpose, string value)
    {
        try { return credentials.ReadRequest(request.TenantId, request.Id, userId, purpose, value); }
        catch (Exception exception) when (exception is CryptographicException or System.Text.Json.JsonException or ArgumentException)
        { throw new ConnectorAccessException("ConsentInvalid", "Restart account-owner consent for this request."); }
    }
    private IConnectorOAuthProvider Provider(string key) => providers.SingleOrDefault(provider => provider.ConnectorKey == key && provider.IsConfigured)
        ?? throw ConnectorAuthorization.Access("ConnectorUnavailable");
    private async Task<ConnectorConnectionRequest> FindAsync(Guid id, CancellationToken cancellationToken)
        => await database.ConnectorConnectionRequests.SingleOrDefaultAsync(request => request.TenantId == authorization.TenantId && request.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Connection request not found.");
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The consent request changed. Reload it before continuing."); }
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static ConnectorAccessException Conflict(string message) => new("Conflict", message);
    private sealed record ConsentResult(ConnectorOAuthTokens Tokens, ConnectorAccountIdentity Account);
}
