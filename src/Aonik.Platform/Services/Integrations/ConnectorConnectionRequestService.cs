using System.Text.RegularExpressions;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorConnectionRequestService(PlatformDbContext database, ConnectorAuthorization authorization,
    ConnectorCatalog catalog, ConnectorPolicyService policies, ConnectorGrantService grants, IEnumerable<IConnectorOAuthProvider> providers,
    IEnumerable<IConnectorActionProvider> actionProviders,
    ITenantRoleReader roles, IPermissionService permissions, IAuditLogWriter audit, IClock clock,
    IWorkflowConnectorContextReader? workflows = null, ConnectorRequestRateLimiter? rateLimiter = null) : IConnectorConnectionRequests
{
    public async Task<ConnectionRequestPreview> PreviewAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default)
        => (await PrepareAsync(request, cancellationToken)).Preview;

    public async Task<ConnectionRequestDetail> CreateAsync(CreateConnectionRequest input, CancellationToken cancellationToken = default)
    {
        Prepared prepared = await PrepareAsync(input, cancellationToken);
        Guid userId = prepared.UserId;
        string inputHash = Hash(new { prepared.Request, prepared.Preview.OwnerUserId });
        if (prepared.Preview.AutomationIdentityId is { } inputActor) inputHash = Hash(new { inputHash, automationIdentityId = inputActor });
        ConnectorConnectionRequest? replay = await database.ConnectorConnectionRequests.AsNoTracking().SingleOrDefaultAsync(request => request.TenantId == authorization.TenantId
            && request.RequesterUserId == userId && request.IdempotencyKey == prepared.Request.IdempotencyKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.InputHash != inputHash) throw Conflict("The request key is already bound to different content.");
            return await DetailAsync(replay, userId, cancellationToken);
        }
        string equivalentHash = Hash(new { tenantId = authorization.TenantId, userId, prepared.Request.ConnectorKey, prepared.Request.Ownership,
            prepared.Preview.OwnerUserId, prepared.Request.Actions, prepared.Request.ConnectionId, prepared.Request.WorkflowId, prepared.Request.DesiredCapability });
        if (prepared.Preview.AutomationIdentityId is { } equivalentActor) equivalentHash = Hash(new { equivalentHash, automationIdentityId = equivalentActor });
        ConnectorConnectionRequest? equivalent = await database.ConnectorConnectionRequests.SingleOrDefaultAsync(request => request.TenantId == authorization.TenantId
            && request.ActiveRequestHash == equivalentHash, cancellationToken);
        if (equivalent is not null && equivalent.ExpiresAt > clock.UtcNow) return await DetailAsync(equivalent, userId, cancellationToken);
        rateLimiter?.Require(authorization.TenantId, userId);
        if (equivalent is not null) Expire(equivalent);
        bool selfService = prepared.Known && prepared.Policy.Code == "Ready" && prepared.Policy.AllowSelfService && prepared.Request.Ownership == "User"
            && Available(prepared.Request.ConnectorKey) && prepared.Preview.AutomationIdentityId is null;
        var request = new ConnectorConnectionRequest
        {
            TenantId = authorization.TenantId, RequesterUserId = userId, ConnectorKey = prepared.Request.ConnectorKey,
            DesiredCapability = prepared.Request.DesiredCapability, Ownership = prepared.Request.Ownership, OwnerUserId = prepared.Preview.OwnerUserId,
            ActionsJson = ConnectorJson.Serialize(prepared.Request.Actions), RequiredScopesJson = ConnectorJson.Serialize(prepared.Preview.RequiredScopes),
            Purpose = prepared.Request.Purpose, WorkflowId = prepared.Request.WorkflowId, RunId = prepared.Request.RunId,
            RequestHash = equivalentHash, ActiveRequestHash = equivalentHash, InputHash = inputHash, IdempotencyKey = prepared.Request.IdempotencyKey,
            Status = selfService ? "AwaitingConsent" : "Pending", ReasonCode = prepared.Preview.ReasonCode,
            NextAction = selfService ? "Complete account-owner consent." : prepared.Preview.NextAction,
            ReviewerRoleId = prepared.Policy.ReviewerRoleId, ExpiresAt = clock.UtcNow.AddDays(prepared.Policy.RequestExpiryDays),
            ConnectionId = prepared.Request.ConnectionId, WorkflowGrantId = prepared.PinnedWorkflowGrantId,
            AutomationIdentityId = prepared.Preview.AutomationIdentityId,
        };
        request.ReviewerUserId = selfService ? null : await ReviewerAsync(request.ReviewerRoleId, cancellationToken, request.AutomationIdentityId.HasValue);
        if (!selfService && request.ReviewerUserId is null) request.NextAction = "No eligible integration reviewer is configured. An integration administrator must assign one.";
        await audit.LogAsync("Connector.ConnectionRequestSubmitted", "ConnectorConnectionRequest", request.Id, request.TenantId, userId, null,
            ConnectorJson.Serialize(prepared.Preview), cancellationToken);
        database.ConnectorConnectionRequests.Add(request);
        var eventIds = new List<Guid> { StageWake(request, "Expire", request.ExpiresAt) };
        Guid? recipient = selfService ? request.OwnerUserId : request.ReviewerUserId;
        if (recipient.HasValue)
        {
            Guid notification = StageWake(request, "Notify", clock.UtcNow, recipient);
            request.ReviewTaskId = notification; eventIds.Add(notification);
        }
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            database.Entry(request).State = EntityState.Detached;
            foreach (var entry in database.ChangeTracker.Entries<OutboxMessage>().Where(entry => eventIds.Contains(entry.Entity.EventId))) entry.State = EntityState.Detached;
            if (equivalent is not null) await database.Entry(equivalent).ReloadAsync(cancellationToken);
            replay = await database.ConnectorConnectionRequests.AsNoTracking().SingleOrDefaultAsync(request => request.TenantId == authorization.TenantId
                && request.RequesterUserId == userId && request.IdempotencyKey == prepared.Request.IdempotencyKey, cancellationToken);
            if (replay is not null)
            {
                if (replay.InputHash != inputHash) throw Conflict("The request key is already bound to different content.");
                return await DetailAsync(replay, userId, cancellationToken);
            }
            equivalent = await database.ConnectorConnectionRequests.AsNoTracking().SingleOrDefaultAsync(request => request.TenantId == authorization.TenantId
                && request.ActiveRequestHash == equivalentHash, cancellationToken);
            if (equivalent is null) throw;
            return await DetailAsync(equivalent, userId, cancellationToken);
        }
        return await DetailAsync(request, userId, cancellationToken);
    }

    public async Task<ConnectionRequestDetail?> GetAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        ConnectorConnectionRequest? request = await FindAsync(requestId, cancellationToken);
        if (request is null || !await CanReadAsync(request, userId, cancellationToken)) return null;
        return await DetailAsync(request, userId, cancellationToken);
    }
    public async Task<IReadOnlyList<ConnectionRequestDetail>> ListAsync(int skip = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        if (skip is < 0 or > 10000 || take is < 1 or > 100) throw new ArgumentException("Use bounded request pagination.");
        bool manager = await authorization.CanManageAsync(userId, cancellationToken);
        Guid[] roleIds = manager ? await database.UserRoles.AsNoTracking().Where(link => !link.IsDeleted && link.UserId == userId && link.Role.TenantId == authorization.TenantId
            && link.User.TenantId == authorization.TenantId).Select(link => link.RoleId).ToArrayAsync(cancellationToken) : [];
        List<ConnectorConnectionRequest> requests = await database.ConnectorConnectionRequests.AsNoTracking().Where(request => request.TenantId == authorization.TenantId
            && (request.RequesterUserId == userId || request.OwnerUserId == userId || (manager && (request.ReviewerRoleId == null || roleIds.Contains(request.ReviewerRoleId.Value)))))
            .OrderByDescending(request => request.CreatedAt).ThenBy(request => request.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        var result = new List<ConnectionRequestDetail>();
        foreach (ConnectorConnectionRequest request in requests) result.Add(await DetailAsync(request, userId, cancellationToken));
        return result;
    }
    public async Task<ConnectionRequestDetail> CancelAsync(Guid requestId, ConnectionRequestCommand command, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Request", cancellationToken);
        ConnectorConnectionRequest request = await FindAsync(requestId, cancellationToken) ?? throw new KeyNotFoundException("Connection request not found.");
        if (request.RequesterUserId != userId) throw new PermissionDeniedException("Integration.Request");
        ConnectorGrantService.Version(request.RowVersion, command.ExpectedVersion);
        RequireActive(request);
        await audit.LogAsync("Connector.ConnectionRequestCancellation", "ConnectorConnectionRequest", request.Id, request.TenantId, userId, null, null, cancellationToken);
        request.Status = "Cancelled"; request.ActiveRequestHash = null; request.NextAction = "The requester cancelled this request.";
        ClearConsent(request);
        await AbandonPendingConnectionAsync(request, cancellationToken);
        await SaveAsync(cancellationToken);
        return await DetailAsync(request, userId, cancellationToken);
    }
    public async Task<ConnectionRequestDetail> DecideAsync(Guid requestId, DecideConnectionRequest command, CancellationToken cancellationToken = default)
    {
        Guid userId = await authorization.RequireUserAsync("Integration.Read", cancellationToken);
        ConnectorConnectionRequest request = await FindAsync(requestId, cancellationToken) ?? throw new KeyNotFoundException("Connection request not found.");
        bool ownerRejection = command.Decision == "Reject" && request.Ownership == "User" && request.OwnerUserId == userId;
        if (ownerRejection) await authorization.RequireActorAsync(new("User", userId, userId), "Integration.Use", cancellationToken);
        else if (!await CanReviewAsync(request, userId, cancellationToken)) throw new PermissionDeniedException("Integration.Manage");
        ConnectorGrantService.Version(request.RowVersion, command.ExpectedVersion);
        RequireActive(request);
        if (request.Status != "Pending" && !(ownerRejection && request.Status == "AwaitingConsent")) throw Conflict("The request is not awaiting an integration review.");
        if (command.Decision is not ("Approve" or "Reject")) throw new ArgumentException("Use Approve or Reject.");
        if (command.ConnectionId is { } selectedId)
        {
            if (command.Decision != "Approve" || request.Ownership != "Tenant" || (request.ConnectionId.HasValue && request.ConnectionId != selectedId))
                throw new ArgumentException("Only an unresolved organizational request can select an existing account. Published bindings cannot change.");
            ConnectorConnection selected = await database.ConnectorConnections.SingleOrDefaultAsync(connection => connection.TenantId == request.TenantId
                && connection.Id == selectedId && connection.ConnectorKey == request.ConnectorKey && connection.Ownership == "Tenant" && connection.Status == "Active", cancellationToken)
                ?? throw new KeyNotFoundException("Eligible organizational connection not found.");
            await grants.RequireManagerAsync(selected, userId, cancellationToken);
            request.ConnectionId = selected.Id;
        }
        await audit.LogAsync("Connector.ConnectionRequestDecision", "ConnectorConnectionRequest", request.Id, request.TenantId, userId, null,
            ConnectorJson.Serialize(new { command.Decision, request.RequestHash, request.ConnectionId }), cancellationToken);
        request.ReviewedByUserId = userId; request.ReviewedAt = clock.UtcNow;
        if (command.Decision == "Reject")
        {
            request.Status = "Rejected"; request.ActiveRequestHash = null; request.NextAction = ownerRejection ? "The account owner rejected this request." : "The integration reviewer rejected this request."; ClearConsent(request);
            await AbandonPendingConnectionAsync(request, cancellationToken);
        }
        else
        {
            Prepared prepared = await PrepareAsync(Input(request), cancellationToken, request.RequesterUserId);
            request.ReasonCode = prepared.Preview.ReasonCode;
            if (!prepared.Known || prepared.Policy.Code != "Ready" || !Available(request.ConnectorKey))
                request.NextAction = "Complete platform/tenant provisioning through the normal integration administration, then review this unchanged request again.";
            else if (request.ConnectionId is { } id && request.Ownership == "Tenant"
                && await database.ConnectorConnections.SingleOrDefaultAsync(connection => connection.TenantId == request.TenantId && connection.Id == id, cancellationToken) is { Status: "Active" } connection)
            {
                IReadOnlyList<string> actualScopes = ConnectorJson.Read<List<string>>(connection.GrantedScopesJson);
                if (ConnectorJson.Read<List<string>>(request.RequiredScopesJson).All(scope => actualScopes.Contains(scope, StringComparer.Ordinal)))
                {
                    (Guid actorGrant, Guid? workflowGrant, Guid? automationGrant) = await grants.StageConsentGrantsAsync(connection, request, userId, cancellationToken);
                    request.GrantId = actorGrant; request.WorkflowGrantId = workflowGrant; request.Status = "Fulfilled"; request.ActiveRequestHash = null;
                    request.AutomationGrantId = automationGrant;
                    request.FulfilledAt = clock.UtcNow; request.FulfilledByUserId = userId;
                    request.NextAction = "Access is ready. Bind/publish or explicitly resume the unchanged workflow after review.";
                }
                else { request.Status = "AwaitingConsent"; request.NextAction = "The organizational account needs consent for the exact requested scopes."; }
            }
            else { request.Status = "AwaitingConsent"; request.NextAction = "The account owner must complete provider consent and confirm the account."; }
        }
        StageWake(request, "Notify", clock.UtcNow, request.RequesterUserId);
        if (request.Status == "AwaitingConsent" && request.OwnerUserId is { } owner && owner != request.RequesterUserId)
            StageWake(request, "Notify", clock.UtcNow, owner);
        await SaveAsync(cancellationToken);
        return await DetailAsync(request, userId, cancellationToken);
    }

    private async Task<Prepared> PrepareAsync(CreateConnectionRequest input, CancellationToken cancellationToken, Guid? requester = null)
    {
        Guid userId = requester ?? await authorization.RequireUserAsync("Integration.Request", cancellationToken);
        await authorization.RequireActorAsync(new("User", userId, userId), "Integration.Request", cancellationToken);
        await authorization.RequireActorAsync(new("User", userId, userId), "Integration.Use", cancellationToken);
        if (input is null || input.ConnectorKey is null || !Regex.IsMatch(input.ConnectorKey, "^[a-z][a-z0-9-]{0,79}$", RegexOptions.NonBacktracking)
            || input.Ownership is not ("User" or "Tenant") || string.IsNullOrWhiteSpace(input.Purpose) || input.Purpose.Length > 1000
            || input.IdempotencyKey is null || !Regex.IsMatch(input.IdempotencyKey, "^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$", RegexOptions.NonBacktracking)
            || input.Actions is null || input.Actions.Count > 100 || input.Actions.Any(action => action is null)) throw new ArgumentException("Provide a valid connector, ownership, purpose, request key and bounded action list.");
        CreateConnectionRequest normalized = input with { Purpose = input.Purpose.Trim(), Actions = input.Actions.Distinct().OrderBy(action => action.Key, StringComparer.Ordinal).ThenBy(action => action.Version).ToList() };
        Guid? pinnedGrant = null;
        Guid? automationIdentityId = null;
        if (normalized.RunId is { } runId)
        {
            if (workflows is null) throw ConnectorAuthorization.Access("WorkflowBindingRequired");
            WorkflowConnectorRequestTarget target = await workflows.GetRequestTargetAsync(runId, userId, cancellationToken);
            if ((normalized.WorkflowId.HasValue && normalized.WorkflowId != target.WorkflowId) || (normalized.ConnectionId.HasValue && normalized.ConnectionId != target.ConnectionId)
                || normalized.Actions.Any(action => !target.AllowedActions.Contains(action))) throw new ArgumentException("A run request must retain its published account and action binding.");
            normalized = normalized with { WorkflowId = target.WorkflowId, ConnectionId = target.ConnectionId };
            ConnectorConnection pinned = await database.ConnectorConnections.AsNoTracking().SingleOrDefaultAsync(connection => connection.TenantId == authorization.TenantId
                && connection.Id == target.ConnectionId && connection.ConnectorKey == normalized.ConnectorKey, cancellationToken) ?? throw new KeyNotFoundException("Pinned connection not found.");
            normalized = normalized with { Ownership = pinned.Ownership };
            pinnedGrant = target.WorkflowGrantId;
            automationIdentityId = target.AutomationIdentityId;
        }
        else if (normalized.WorkflowId is { } workflowId)
        {
            if (workflows is null) throw ConnectorAuthorization.Access("WorkflowBindingRequired");
            await workflows.RequireGrantTargetAsync(workflowId, userId, cancellationToken);
        }
        bool known = catalog.Find(normalized.ConnectorKey) is not null;
        if (!known && normalized.ConnectionId.HasValue) throw new ArgumentException("An unavailable connector cannot bind an executable account.");
        ConnectorPolicy policy = await policies.GetAsync(known ? normalized.ConnectorKey : "gmail", cancellationToken);
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        if (known)
        {
            if (normalized.Actions.Count == 0) throw new ArgumentException("Select at least one registered action/version.");
            if (policy.Code == "ForbiddenByPolicy" || (normalized.Ownership == "User" && !policy.AllowUserConnections)
                || (normalized.WorkflowId.HasValue && !policy.AllowWorkflowConnections)) throw ConnectorAuthorization.Access("ForbiddenByPolicy");
            foreach (ConnectorActionRef action in normalized.Actions)
            {
                ConnectorActionDescriptor descriptor = catalog.Resolve(normalized.ConnectorKey, action);
                if (!policy.AllowedActions.Contains(action)) throw ConnectorAuthorization.Access("ForbiddenByPolicy");
                scopes.UnionWith(descriptor.RequiredScopes);
            }
            scopes.UnionWith(["openid", "email"]);
        }
        else if (normalized.Actions.Count != 0 || string.IsNullOrWhiteSpace(normalized.DesiredCapability) || normalized.DesiredCapability.Length > 500)
            throw new ArgumentException("An unavailable provider request needs a desired capability and must not invent action versions or scopes.");
        Guid? ownerUserId = normalized.Ownership == "User" ? userId : null;
        if (normalized.ConnectionId is { } connectionId)
        {
            ConnectorConnection connection = await database.ConnectorConnections.AsNoTracking().SingleOrDefaultAsync(connection => connection.TenantId == authorization.TenantId
                && connection.Id == connectionId && connection.ConnectorKey == normalized.ConnectorKey, cancellationToken) ?? throw new KeyNotFoundException("Connection not found.");
            if (connection.Ownership != normalized.Ownership
                || (!requester.HasValue && normalized.RunId is null && connection.OwnerUserId != userId && !await authorization.CanManageAsync(userId, cancellationToken)
                    && !await authorization.HasActorGrantAsync(connection, new("User", userId, userId), normalized.Actions[0], cancellationToken)))
                throw new PermissionDeniedException("Integration.Request");
            ownerUserId = connection.OwnerUserId;
            if (connection.Ownership == "User" && (ownerUserId is null || !await authorization.IsActiveAsync(ownerUserId.Value, cancellationToken)))
                throw ConnectorAuthorization.Access("NoAuthorizedConnection");
            if (!requester.HasValue && connection.Status == "PendingConsent") throw Conflict("Complete or cancel the existing account consent request first.");
        }
        string reason = !known || !Available(normalized.ConnectorKey)
            ? "ConnectorUnavailable" : policy.Code != "Ready" ? policy.Code : normalized.ConnectionId.HasValue ? "ConnectionGrantRequired" : "NoAuthorizedConnection";
        return new(normalized, userId, policy, known, pinnedGrant, new(normalized.ConnectorKey, normalized.Ownership, ownerUserId,
            normalized.Actions, scopes.Order(StringComparer.Ordinal).ToList(), normalized.Purpose, normalized.WorkflowId, normalized.RunId, reason,
            ConnectorService.Readiness(reason).NextAction, policy.ReviewerRoleId, normalized.DesiredCapability, automationIdentityId));
    }

    internal async Task<bool> CanReviewAsync(ConnectorConnectionRequest request, Guid userId, CancellationToken cancellationToken)
        => await authorization.CanManageAsync(userId, cancellationToken)
            && (request.AutomationIdentityId is null || await permissions.HasPermissionAsync(userId, "Workflow.Write", cancellationToken))
            && (request.ReviewerRoleId is null
            || await roles.IsMemberAsync(request.TenantId, userId, request.ReviewerRoleId.Value, cancellationToken));
    private async Task<bool> CanReadAsync(ConnectorConnectionRequest request, Guid userId, CancellationToken cancellationToken)
        => request.RequesterUserId == userId || request.OwnerUserId == userId || await CanReviewAsync(request, userId, cancellationToken);
    internal async Task<ConnectionRequestDetail> DetailAsync(ConnectorConnectionRequest request, Guid userId, CancellationToken cancellationToken)
    {
        bool active = request.ExpiresAt > clock.UtcNow && request.Status is "Pending" or "AwaitingConsent";
        bool review = active && request.Status == "Pending" && await CanReviewAsync(request, userId, cancellationToken);
        bool connect = active && request.Status == "AwaitingConsent" && (request.Ownership == "User" ? request.OwnerUserId == userId
            : await CanReviewAsync(request, userId, cancellationToken));
        return new(request.Id, request.ConnectorKey, request.RequesterUserId, request.Ownership, request.OwnerUserId,
            ConnectorJson.Read<List<ConnectorActionRef>>(request.ActionsJson), ConnectorJson.Read<List<string>>(request.RequiredScopesJson), request.Purpose,
            !active && request.ExpiresAt <= clock.UtcNow && request.Status is "Pending" or "AwaitingConsent" ? "Expired" : request.Status,
            request.ReasonCode, request.NextAction, request.WorkflowId, request.RunId, request.ConnectionId, request.GrantId, request.WorkflowGrantId,
            request.ReviewerRoleId, request.ExpiresAt, Convert.ToBase64String(request.RowVersion), connect, review,
            request.OwnerUserId == userId || request.RequesterUserId == userId ? $"/settings/my-connections?requestId={request.Id:D}" : $"/settings/tenant-integrations?requestId={request.Id:D}",
            connect ? request.ConsentedAccountLabel : null, request.DesiredCapability,
            connect && request.OAuthStartedByUserId == userId && request.ProtectedOAuthResult is not null && request.OAuthExpiresAt > clock.UtcNow,
            active && request.RequesterUserId == userId, request.AutomationIdentityId, request.AutomationGrantId);
    }
    internal async Task RequireConsentPolicyAsync(ConnectorConnectionRequest request, CancellationToken cancellationToken)
    {
        Prepared prepared = await PrepareAsync(Input(request), cancellationToken, request.RequesterUserId);
        if (prepared.Preview.OwnerUserId != request.OwnerUserId || prepared.Preview.Ownership != request.Ownership
            || prepared.Preview.AutomationIdentityId != request.AutomationIdentityId)
            throw Conflict("The account ownership changed. Review a new request.");
        if (!prepared.Known || !Available(request.ConnectorKey))
            throw ConnectorAuthorization.Access("ConnectorUnavailable");
        if (prepared.Policy.Code != "Ready") throw ConnectorAuthorization.Access(prepared.Policy.Code);
    }
    private async Task<Guid?> ReviewerAsync(Guid? roleId, CancellationToken cancellationToken, bool automationRequest = false)
    {
        if (roleId is null || !await roles.ExistsAsync(authorization.TenantId, roleId.Value, cancellationToken)) return null;
        List<Guid> candidates = await database.UserRoles.AsNoTracking().Where(link => !link.IsDeleted && link.RoleId == roleId && link.Role.TenantId == authorization.TenantId
            && link.User.TenantId == authorization.TenantId && link.User.Status == "Active").Select(link => link.UserId).Order().Take(100).ToListAsync(cancellationToken);
        foreach (Guid candidate in candidates)
            if (await permissions.HasPermissionAsync(candidate, "Integration.Manage", cancellationToken)
                && (!automationRequest || await permissions.HasPermissionAsync(candidate, "Workflow.Write", cancellationToken))) return candidate;
        return null;
    }
    internal Guid StageWake(ConnectorConnectionRequest request, string kind, DateTime due, Guid? recipient = null)
    {
        Guid id = Guid.NewGuid();
        database.EnqueueIntegrationEvent(new ConnectorRequestWakeRequestedEvent(id, clock.UtcNow, request.TenantId, request.Id, kind,
            DateTime.SpecifyKind(due, DateTimeKind.Utc), recipient));
        return id;
    }
    internal void RequireActive(ConnectorConnectionRequest request)
    {
        if (request.ExpiresAt <= clock.UtcNow || request.Status is not ("Pending" or "AwaitingConsent")) throw Conflict("The request is terminal or expired.");
    }
    internal static void ClearConsent(ConnectorConnectionRequest request)
    {
        request.OAuthStateHash = null; request.ProtectedVerifier = null; request.ProtectedOAuthResult = null;
        request.OAuthLeaseId = null; request.OAuthLeaseExpiresAt = null;
    }
    internal static void Expire(ConnectorConnectionRequest request)
    { request.Status = "Expired"; request.ActiveRequestHash = null; request.NextAction = "This connection request expired."; ClearConsent(request); }
    internal async Task AbandonPendingConnectionAsync(ConnectorConnectionRequest request, CancellationToken cancellationToken)
    {
        if (request.ConnectionId is not { } id) return;
        ConnectorConnection? connection = await database.ConnectorConnections.SingleOrDefaultAsync(connection => connection.TenantId == request.TenantId
            && connection.Id == id && connection.Status == "PendingConsent", cancellationToken);
        if (connection is not null) { connection.Status = "Revoked"; connection.LastErrorCode = "ConsentRequestClosed"; }
    }
    private Task<ConnectorConnectionRequest?> FindAsync(Guid id, CancellationToken cancellationToken) => database.ConnectorConnectionRequests
        .SingleOrDefaultAsync(request => request.TenantId == authorization.TenantId && request.Id == id, cancellationToken);
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw Conflict("The request changed concurrently. Reload it before deciding."); }
    }
    private static string Hash<T>(T value) => ToolArgumentsHash.Compute(new Dictionary<string, object?> { ["request"] = ConnectorJson.Element(value) });
    private static CreateConnectionRequest Input(ConnectorConnectionRequest request) => new(request.ConnectorKey, request.Ownership,
        ConnectorJson.Read<List<ConnectorActionRef>>(request.ActionsJson), request.Purpose, request.IdempotencyKey, request.ConnectionId, request.WorkflowId, request.RunId, request.DesiredCapability);
    private static ConnectorAccessException Conflict(string message) => new("Conflict", message);
    private bool Available(string key) => providers.SingleOrDefault(provider => provider.ConnectorKey == key)?.IsConfigured == true
        && actionProviders.Any(provider => provider.ConnectorKey == key);
    private sealed record Prepared(CreateConnectionRequest Request, Guid UserId, ConnectorPolicy Policy, bool Known, Guid? PinnedWorkflowGrantId, ConnectionRequestPreview Preview);
}
