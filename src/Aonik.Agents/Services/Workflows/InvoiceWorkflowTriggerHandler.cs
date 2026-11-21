using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Services.Workflows;

internal sealed class InvoiceWorkflowTriggerHandler(AgentsDbContext database, ITenantProvider tenantProvider, IClock clock, WorkflowTriggerTaskProtector proofs)
    : IEventHandler<InvoiceLifecycleChangedEvent>
{
    public async Task HandleAsync(InvoiceLifecycleChangedEvent message, CancellationToken cancellationToken = default)
    {
        if (message.TenantId != tenantProvider.GetCurrentTenantId() || message.EventId == Guid.Empty || message.InvoiceId == Guid.Empty
            || message.OccurredAt.Kind != DateTimeKind.Utc || message.OccurredAt > clock.UtcNow.AddMinutes(5)
            || message.Transition is not ("Created" or "Issued")) throw new ArgumentException("Invalid invoice trigger event scope.");
        string source = message.Transition == "Created" ? "InvoiceCreated" : "InvoiceIssued";
        List<WorkflowTriggerBinding> bindings = await database.WorkflowTriggerBindings.AsNoTracking().Where(binding => binding.TenantId == message.TenantId
            && binding.Status == "Active" && binding.ActiveSource == source && binding.ActivatedAt <= message.OccurredAt).OrderBy(binding => binding.Id)
            .Take(1001).ToListAsync(cancellationToken);
        if (bindings.Count > 1000) throw new InvalidOperationException("Invoice trigger fan-out exceeds one thousand bindings; the durable source event requires operator review.");
        foreach (WorkflowTriggerBinding binding in bindings)
        {
            WorkflowTriggerDefinition definition = WorkflowTriggerService.Read(binding.ActiveJson!);
            if (definition.Subject.Mapping == "Fixed" && !definition.Subject.FixedIds.Contains(message.InvoiceId)) continue;
            Guid eventId = Guid.NewGuid();
            bool selfGenerated = message.CausationRunId.HasValue;
            var payload = new WorkflowTriggerAdmissionPayload(binding.Id, binding.ActiveRevision!.Value, message.EventId, message.OccurredAt, "Invoice", message.InvoiceId, message.CausationRunId, selfGenerated);
            database.EnqueueIntegrationEvent(new WorkflowTriggerAdmissionRequestedEvent(eventId, clock.UtcNow, message.TenantId, binding.Id,
                binding.ActiveRevision.Value, message.EventId, message.OccurredAt, "Invoice", message.InvoiceId, message.CausationRunId,
                proofs.ProtectAdmission(payload, message.TenantId, eventId), selfGenerated));
        }
        await database.SaveChangesAsync(cancellationToken);
    }
}
