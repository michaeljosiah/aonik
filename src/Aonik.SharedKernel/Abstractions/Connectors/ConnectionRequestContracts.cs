namespace Aonik.SharedKernel.Abstractions.Connectors;

public sealed record CreateConnectionRequest(string ConnectorKey, string Ownership, IReadOnlyList<ConnectorActionRef> Actions,
    string Purpose, string IdempotencyKey, Guid? ConnectionId = null, Guid? WorkflowId = null, Guid? RunId = null, string? DesiredCapability = null);
public sealed record ConnectionRequestPreview(string ConnectorKey, string Ownership, Guid? OwnerUserId, IReadOnlyList<ConnectorActionRef> Actions,
    IReadOnlyList<string> RequiredScopes, string Purpose, Guid? WorkflowId, Guid? RunId, string ReasonCode, string NextAction, Guid? ReviewerRoleId,
    string? DesiredCapability = null);
public sealed record ConnectionRequestDetail(Guid Id, string ConnectorKey, Guid RequesterUserId, string Ownership, Guid? OwnerUserId,
    IReadOnlyList<ConnectorActionRef> Actions, IReadOnlyList<string> RequiredScopes, string Purpose, string Status, string ReasonCode, string NextAction,
    Guid? WorkflowId, Guid? RunId, Guid? ConnectionId, Guid? GrantId, Guid? WorkflowGrantId, Guid? ReviewerRoleId, DateTime ExpiresAt,
    string ExpectedVersion, bool CanConnect, bool CanReview, string ReviewPath, string? ConsentedAccountLabel = null, string? DesiredCapability = null,
    bool CanConfirm = false, bool CanCancel = false);
public sealed record ConnectionRequestCommand(string ExpectedVersion);
public sealed record DecideConnectionRequest(string ExpectedVersion, string Decision, Guid? ConnectionId = null);
public sealed record ConnectorGrantCommand(string GranteeType, Guid GranteeId, IReadOnlyList<ConnectorActionRef> Actions,
    string ExpectedConnectionVersion, Guid? ExistingGrantId = null, string? ExpectedGrantVersion = null, DateTime? ExpiresAt = null);
public sealed record ConnectorGrantDetail(Guid Id, Guid ConnectionId, string GranteeType, Guid GranteeId,
    IReadOnlyList<ConnectorActionRef> Actions, DateTime? ExpiresAt, DateTime? RevokedAt, string ExpectedVersion);

public interface IConnectorConnectionRequests
{
    Task<ConnectionRequestPreview> PreviewAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default);
    Task<ConnectionRequestDetail> CreateAsync(CreateConnectionRequest request, CancellationToken cancellationToken = default);
    Task<ConnectionRequestDetail?> GetAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConnectionRequestDetail>> ListAsync(int skip = 0, int take = 20, CancellationToken cancellationToken = default);
    Task<ConnectionRequestDetail> CancelAsync(Guid requestId, ConnectionRequestCommand command, CancellationToken cancellationToken = default);
    Task<ConnectionRequestDetail> DecideAsync(Guid requestId, DecideConnectionRequest command, CancellationToken cancellationToken = default);
}
