using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Database.Tests.Support;

/// <summary>
/// The out-of-module collaborators the PersonalFinance transaction paths inject.
/// All are deliberately inert: this lane asserts relational-provider semantics
/// (execution strategy, transactions, rowversion), not side effects — the
/// InMemory suites in Aonik.Application.Tests own the behavioural assertions.
/// Mirrors the private fakes those suites use.
/// </summary>
internal sealed class FixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc);
}

internal sealed class FakeTaskService : ITaskService
{
    public Task<TaskResponse> ScheduleAsync(ScheduleTaskRequest request, CancellationToken ct = default) => Task.FromResult<TaskResponse>(null!);
    public Task<TaskResponse?> GetAsync(Guid taskId, CancellationToken ct = default) => Task.FromResult<TaskResponse?>(null);
    public Task<IReadOnlyList<TaskResponse>> ListForSubjectAsync(string subjectType, Guid subjectId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TaskResponse>>([]);
    public Task<IReadOnlyList<TaskResponse>> ListForAssigneeAsync(string assigneeType, Guid? assigneeId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TaskResponse>>([]);
    public Task PauseAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
    public Task ResumeAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
    public Task CancelAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class NoOpGraphCacheInvalidator : IFinancialLifeGraphCacheInvalidator
{
    public void InvalidateCurrentUserGraph()
    {
    }

    public Task InvalidateCurrentUserGraphAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InvalidateUserGraphAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InvalidateUserGraphsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class NoOpNotificationWriter : IUserNotificationWriter
{
    public Task WriteForUserAsync(UserNotificationWriteRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class StubPartyReader : IPartyReader
{
    public Dictionary<Guid, PartyHistoryItem> Parties { get; } = [];

    public Task<IReadOnlyList<PartyHistoryItem>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> partyIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PartyHistoryItem>>(partyIds.Where(Parties.ContainsKey).Select(id => Parties[id]).ToList());

    public Task<IReadOnlyList<PartyRelationshipHistoryItem>> GetRelationshipsForPartyAsync(Guid tenantId, Guid partyId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PartyRelationshipHistoryItem>>([]);

    public Task<bool> ExistsAsync(Guid tenantId, Guid partyId, CancellationToken ct = default)
        => Task.FromResult(Parties.ContainsKey(partyId));

    public Task<bool> HasActiveRelationshipBetweenAsync(Guid tenantId, Guid a, Guid b, CancellationToken ct = default)
        => Task.FromResult(false);

    public Task<Guid?> GetTenantPartyIdAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<Guid?>(Parties.Keys.OrderBy(id => id).Cast<Guid?>().FirstOrDefault());
}

internal sealed class StubUserDirectoryReader : IUserDirectoryReader
{
    public Dictionary<Guid, UserDirectoryItem> Users { get; } = [];

    public Task<IReadOnlyList<UserDirectoryItem>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UserDirectoryItem>>(userIds.Where(Users.ContainsKey).Select(id => Users[id]).ToList());

    public Task<IReadOnlyList<UserDirectoryKey>> GetAllUserKeysAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UserDirectoryKey>>([]);
}
