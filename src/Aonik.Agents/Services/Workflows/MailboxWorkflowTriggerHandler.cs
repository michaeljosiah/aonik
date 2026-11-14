using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Services.Workflows;

internal sealed class MailboxWorkflowTriggerHandler(AgentsDbContext database, ITenantProvider tenantProvider, IWorkflowMailboxBindingReader authority,
    IClock clock, WorkflowTriggerTaskProtector proofs, IFinancialDocumentMailboxIntake? documents = null) : IEventHandler<ConnectorMessageAddedEvent>
{
    public async Task HandleAsync(ConnectorMessageAddedEvent message, CancellationToken cancellationToken = default)
    {
        if (message.TenantId != tenantProvider.GetCurrentTenantId() || message.EventId == Guid.Empty || message.ConnectionId == Guid.Empty
            || message.OccurredAt.Kind != DateTimeKind.Utc || message.OccurredAt > clock.UtcNow.AddMinutes(5) || message.ReceivedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid normalized mailbox event scope.");
        var bindings = await database.WorkflowTriggerBindings.AsNoTracking().Where(binding => binding.TenantId == message.TenantId && binding.Status == "Active"
            && binding.ActiveSource == "GmailMessageAdded").OrderBy(binding => binding.Id).Take(1001).ToListAsync(cancellationToken);
        if (bindings.Count > 1000) throw new InvalidOperationException("Mailbox trigger fan-out exceeds one thousand bindings.");
        foreach (var binding in bindings)
        {
            var definition = WorkflowTriggerService.Read(binding.ActiveJson!);
            var mailbox = definition.Mailbox!;
            if (mailbox.ConnectionId != message.ConnectionId || (message.SelfGenerated && definition.Limits.IgnoreSelfGenerated)
                || message.ReceivedAtUtc < (mailbox.BackfillSinceUtc ?? binding.ActivatedAt)
                || mailbox.LabelIds.Any(label => !message.LabelIds.Contains(label, StringComparer.Ordinal))) continue;
            try { _ = await authority.RequireActiveAsync(binding.Id, binding.ActiveRevision!.Value, cancellationToken); }
            catch (Exception exception) when (exception is PermissionDeniedException or ConnectorAccessException) { continue; }
            IReadOnlyList<Guid> subjectIds = definition.Subject.Type == "Document"
                ? await (documents ?? throw new InvalidOperationException("Financial document intake is unavailable.")).AdmitAsync(binding.Id,
                    binding.ActiveRevision.Value, message.MessageId, cancellationToken) : definition.Subject.FixedIds;
            foreach (Guid subjectId in subjectIds)
            {
                Guid taskId = Guid.NewGuid();
                var payload = new WorkflowTriggerAdmissionPayload(binding.Id, binding.ActiveRevision!.Value, message.EventId, message.OccurredAt,
                    definition.Subject.Type, subjectId, message.CausationRunId, message.SelfGenerated);
                database.EnqueueIntegrationEvent(new WorkflowTriggerAdmissionRequestedEvent(taskId, clock.UtcNow, message.TenantId, binding.Id,
                    binding.ActiveRevision.Value, message.EventId, message.OccurredAt, definition.Subject.Type, subjectId, message.CausationRunId,
                    proofs.ProtectAdmission(payload, message.TenantId, taskId), message.SelfGenerated));
            }
        }
        await database.SaveChangesAsync(cancellationToken);
    }
}
