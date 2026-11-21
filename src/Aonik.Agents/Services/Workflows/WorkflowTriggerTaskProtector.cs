using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.AspNetCore.DataProtection;

namespace Aonik.Agents.Services.Workflows;

/// <summary>A generic Tasks.Write caller cannot fabricate a trusted source event or substitute its invoice.</summary>
internal sealed class WorkflowTriggerTaskProtector(IDataProtectionProvider protection)
{
    public string ProtectAdmission(WorkflowTriggerAdmissionPayload payload, Guid tenantId, Guid taskId)
        => Protect("admission", payload, tenantId, taskId);
    public WorkflowTriggerAdmissionPayload ReadAdmission(string envelope, Guid tenantId, Guid taskId)
        => Read<WorkflowTriggerAdmissionPayload>("admission", envelope, tenantId, taskId);
    public string ProtectSchedule(WorkflowTriggerSchedulePayload payload, Guid tenantId, Guid taskId)
        => Protect("schedule", payload, tenantId, taskId);
    public WorkflowTriggerSchedulePayload ReadSchedule(string envelope, Guid tenantId, Guid taskId)
        => Read<WorkflowTriggerSchedulePayload>("schedule", envelope, tenantId, taskId);

    private IDataProtector Protector(string purpose, Guid tenantId, Guid taskId)
    {
        if (tenantId == Guid.Empty || taskId == Guid.Empty) throw new ArgumentException("A trigger task requires exact tenant and task identities.");
        return protection.CreateProtector("Aonik.Workflows.TriggerTask.v1", purpose, tenantId.ToString("N"), taskId.ToString("N"));
    }
    private string Protect<T>(string purpose, T payload, Guid tenantId, Guid taskId) => JsonSerializer.Serialize(new ProtectedTriggerTask(
        Protector(purpose, tenantId, taskId).Protect(JsonSerializer.Serialize(payload, WorkflowDefinitionCodec.JsonOptions))), WorkflowDefinitionCodec.JsonOptions);
    private T Read<T>(string purpose, string envelope, Guid tenantId, Guid taskId)
    {
        try
        {
            if (envelope.Length > 16_384) throw new ArgumentException("Trigger task proof exceeds its size limit.");
            ProtectedTriggerTask parsed = JsonSerializer.Deserialize<ProtectedTriggerTask>(envelope, WorkflowDefinitionCodec.JsonOptions)
                ?? throw new ArgumentException("Trigger task proof is required.");
            return JsonSerializer.Deserialize<T>(Protector(purpose, tenantId, taskId).Unprotect(parsed.Proof), WorkflowDefinitionCodec.JsonOptions)
                ?? throw new ArgumentException("Trigger task proof is invalid.");
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        { throw new ArgumentException("The source, tenant or task identity does not match its server-issued trigger proof."); }
    }
    internal static Guid Identity(string purpose, params object[] values) => Guid.ParseExact(WorkflowDefinitionCodec.Hash(
        JsonSerializer.Serialize(new { purpose, values }, WorkflowDefinitionCodec.JsonOptions))[..32], "N");
    private sealed record ProtectedTriggerTask(string Proof);
}
internal sealed record WorkflowTriggerAdmissionPayload(Guid BindingId, int Revision, Guid SourceEventId, DateTime SourceOccurredAtUtc,
    string SubjectType, Guid SubjectId, Guid? CausationRunId, bool SelfGenerated = false);
internal sealed record WorkflowTriggerSchedulePayload(Guid BindingId, int Revision);
