using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Workflows;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowMailboxBindingReader(AgentsDbContext database, ITenantProvider tenantProvider, WorkflowExecutionAuthorization execution,
    IConnectorService connectors, IAutomationIdentityAuthorization? automation = null)
    : IWorkflowMailboxBindingReader
{
    public async Task<ConnectorSubscriptionBinding> RequireActiveAsync(Guid bindingId, int revision, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        var binding = await database.WorkflowTriggerBindings.AsNoTracking().SingleOrDefaultAsync(binding => binding.TenantId == tenantId && binding.Id == bindingId
            && binding.Status == "Active" && binding.ActiveRevision == revision && binding.ActiveSource == "GmailMessageAdded", cancellationToken) ?? throw Denied();
        if (binding.ActiveJson is null || WorkflowDefinitionCodec.Hash(binding.ActiveJson) != binding.ActiveHash) throw Denied();
        var proposal = await database.Proposals.AsNoTracking().SingleOrDefaultAsync(proposal => proposal.TenantId == tenantId && proposal.Id == binding.ActivationProposalId
            && proposal.ProposalType == WorkflowTriggerApprovalManifest.ActivateType && proposal.Status == Entities.ProposalStatus.Applied && proposal.ApprovedByUserId != null, cancellationToken) ?? throw Denied();
        JsonElement payload = JsonSerializer.Deserialize<JsonElement>(proposal.PayloadJson);
        if (payload.GetProperty("bindingId").GetGuid() != binding.Id || payload.GetProperty("command").GetProperty("definitionHash").GetString() != binding.ActiveHash) throw Denied();
        var definition = WorkflowTriggerService.Read(binding.ActiveJson);
        var mailbox = definition.Mailbox ?? throw Denied();
        if (!await database.Workflows.AsNoTracking().AnyAsync(workflow => workflow.TenantId == tenantId && workflow.Id == binding.WorkflowId
            && workflow.State == Entities.Workflows.WorkflowStates.Active && workflow.RetiredAt == null, cancellationToken)) throw Denied();
        var version = await database.WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(version => version.TenantId == tenantId && version.Id == definition.WorkflowVersionId
            && version.WorkflowId == binding.WorkflowId, cancellationToken) ?? throw Denied();
        var snapshot = WorkflowRunService.ReadDefinition(version);
        if (WorkflowSubjectContract.Type(snapshot) != definition.Subject.Type) throw Denied();
        await execution.RequireSubjectModuleAsync(definition.Subject.Type, cancellationToken);
        await execution.RequireEnabledAsync(cancellationToken);
        await execution.RequireAutomaticEnabledAsync(cancellationToken);
        var actor = new WorkflowActor("AutomationIdentity", definition.AutomationIdentityId, WorkflowVersionId: version.Id);
        await execution.RequireActorAsync(actor, cancellationToken);
        if (definition.Subject.Type == WorkflowSubjects.Document)
        {
            if (automation is null || definition.Subject.Mapping != "MessageAttachments") throw Denied();
            await automation.RequireAllSubjectsAsync(actor, WorkflowSubjects.Document, cancellationToken);
        }
        foreach (Guid subjectId in definition.Subject.FixedIds) await execution.LoadFactsAsync(definition.Subject.Type, subjectId, actor, cancellationToken);
        var connection = new WorkflowConnectorBinding(mailbox.ConnectionId, mailbox.WorkflowGrantId,
            definition.Subject.Type == WorkflowSubjects.Document ? GmailActionKeys.ReadActions.Select(key => new ConnectorActionRef(key, 1)).ToArray() : [new(GmailActionKeys.Search, 1)]);
        await connectors.ValidateWorkflowBindingAsync(binding.WorkflowId, connection, new(GmailActionKeys.Search, 1), actor, cancellationToken);
        return new(binding.Id, revision, binding.WorkflowId, version.Id, definition.AutomationIdentityId,
            connection, mailbox.Mode, mailbox.PollIntervalMinutes,
            mailbox.LabelIds, mailbox.AllowPollingFallback, mailbox.BackfillSinceUtc, binding.ActivatedAt,
            definition.Subject.Type, mailbox.SubjectPrefix, definition.Limits.IgnoreSelfGenerated);
    }

    public Task<bool> IsDisabledAsync(Guid bindingId, CancellationToken cancellationToken = default)
        => database.WorkflowTriggerBindings.AsNoTracking().AnyAsync(binding => binding.TenantId == tenantProvider.GetCurrentTenantId()
            && binding.Id == bindingId && binding.Status == "Disabled", cancellationToken);
    private static PermissionDeniedException Denied() => new("Workflow.Run", "Mailbox synchronization requires an intact active and approved trigger binding.");
}
