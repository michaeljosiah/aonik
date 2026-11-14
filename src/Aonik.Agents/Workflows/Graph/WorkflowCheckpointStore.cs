using System.Reflection;
using System.Text;
using System.Text.Json;

using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Agents.Workflows.Graph;

/// <summary>Stores MAF's serialized state; graph control flow remains in MAF.</summary>
internal sealed class WorkflowCheckpointStore(AgentsDbContext database, WorkflowRun run, Guid leaseId, IClock clock,
    CancellationToken cancellationToken = default)
    : ICheckpointStore<JsonElement>
{
    internal const int FormatVersion = 1;
    internal static readonly string FrameworkVersion = typeof(Microsoft.Agents.AI.Workflows.Workflow).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

    public async ValueTask<CheckpointInfo> CreateCheckpointAsync(string sessionId, JsonElement value, CheckpointInfo? parent = null)
    {
        await RequireLeaseAsync();
        CheckSession(sessionId);
        if (parent is not null && (parent.SessionId != sessionId || !await database.WorkflowCheckpoints.AsNoTracking().AnyAsync(
            checkpoint => checkpoint.TenantId == run.TenantId && checkpoint.RunId == run.Id && checkpoint.CheckpointId == parent.CheckpointId,
            cancellationToken))) throw new WorkflowExecutionException("CheckpointScopeMismatch");
        string json = value.GetRawText();
        if (Encoding.UTF8.GetByteCount(json) > 8 * 1024 * 1024)
            throw new WorkflowExecutionException("CheckpointTooLarge");
        long ordinal = (await database.WorkflowCheckpoints.Where(checkpoint => checkpoint.TenantId == run.TenantId && checkpoint.RunId == run.Id)
            .MaxAsync(checkpoint => (long?)checkpoint.Sequence, cancellationToken) ?? 0) + 1;
        var saved = new WorkflowCheckpoint
        {
            TenantId = run.TenantId, RunId = run.Id, WorkflowVersionId = run.WorkflowVersionId!.Value,
            FrameworkSessionId = sessionId, CheckpointId = Guid.NewGuid().ToString("N"),
            ParentCheckpointId = parent?.CheckpointId, Sequence = ordinal,
            FormatVersion = FormatVersion, FrameworkVersion = FrameworkVersion, StateJson = json,
        };
        database.WorkflowCheckpoints.Add(saved);
        run.FrameworkSessionId = sessionId;
        run.CheckpointId = saved.CheckpointId;
        await database.SaveChangesAsync(cancellationToken);
        return new(sessionId, saved.CheckpointId);
    }

    public async ValueTask<JsonElement> RetrieveCheckpointAsync(string sessionId, CheckpointInfo key)
    {
        CheckSession(sessionId);
        if (key.SessionId != sessionId) throw new WorkflowExecutionException("CheckpointScopeMismatch");
        WorkflowCheckpoint saved = await database.WorkflowCheckpoints.AsNoTracking().SingleOrDefaultAsync(
            checkpoint => checkpoint.TenantId == run.TenantId && checkpoint.RunId == run.Id
                && checkpoint.FrameworkSessionId == sessionId && checkpoint.CheckpointId == key.CheckpointId, cancellationToken)
            ?? throw new WorkflowExecutionException("CheckpointMissing");
        if (saved.WorkflowVersionId != run.WorkflowVersionId || saved.FormatVersion != FormatVersion || saved.FrameworkVersion != FrameworkVersion)
            throw new WorkflowExecutionException("CheckpointIncompatible");
        return JsonSerializer.Deserialize<JsonElement>(saved.StateJson);
    }

    public async ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(string sessionId, CheckpointInfo? withParent = null)
    {
        CheckSession(sessionId);
        if (withParent is not null && withParent.SessionId != sessionId) throw new WorkflowExecutionException("CheckpointScopeMismatch");
        return (await database.WorkflowCheckpoints.AsNoTracking().Where(checkpoint => checkpoint.TenantId == run.TenantId
                && checkpoint.RunId == run.Id && checkpoint.FrameworkSessionId == sessionId
                && (withParent == null || checkpoint.ParentCheckpointId == withParent.CheckpointId))
            .OrderBy(checkpoint => checkpoint.Sequence).Select(checkpoint => checkpoint.CheckpointId).ToListAsync(cancellationToken))
            .Select(id => new CheckpointInfo(sessionId, id));
    }

    private void CheckSession(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || sessionId.Length > 128 || !run.WorkflowVersionId.HasValue
            || (run.FrameworkSessionId is not null && run.FrameworkSessionId != sessionId))
            throw new WorkflowExecutionException("CheckpointScopeMismatch");
    }

    private async Task RequireLeaseAsync()
    {
        if (!await database.WorkflowRuns.AsNoTracking().AnyAsync(current => current.TenantId == run.TenantId && current.Id == run.Id
            && current.LeaseId == leaseId && current.LeaseExpiresAt > clock.UtcNow, cancellationToken))
            throw new WorkflowExecutionException("RunLeaseLost");
    }
}
