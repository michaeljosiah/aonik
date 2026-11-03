namespace Aonik.SharedKernel.Abstractions.Platform;

/// <summary>Tenant-scoped, active staff directory for explicitly shared conversations.</summary>
public interface IChatParticipantDirectory
{
    Task<IReadOnlyList<ChatWorkspacePerson>> SearchAsync(Guid tenantId, string? search, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatWorkspacePerson>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}

public sealed record ChatWorkspacePerson(Guid UserId, string DisplayName, bool CanRequestAgentActions = false);
