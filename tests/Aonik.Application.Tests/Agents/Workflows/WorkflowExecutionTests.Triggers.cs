using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Finance.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Application.Tests.Agents.Workflows;

public partial class WorkflowExecutionTests
{
    [Fact]
    public async Task TriggerTools_Should_RequireExactDraftConfirmation_AndQueueActivationWithoutRunningItInline()
    {
        using var host = new Host(); (WorkflowTriggerDetail source, _) = await ActivateTriggerAsync(host);
        using IServiceScope scope = host.Scope(); IServiceProvider services = scope.ServiceProvider;
        var arguments = new Dictionary<string, object?> { ["slug"] = "invoice-route", ["bindingId"] = null,
            ["request"] = new SaveWorkflowTriggerRequest("Another route", source.Draft) };
        AIFunction save = WorkflowTriggerTools.CreateAll(services).OfType<AIFunction>().Single(tool => tool.Name == "aonik_save_workflow_trigger");
        ToolApprovalRequiredResult pending = ToolResult<ToolApprovalRequiredResult>(await save.InvokeAsync(new AIFunctionArguments(arguments)));
        (await services.GetRequiredService<AgentsDbContext>().WorkflowTriggerBindings.CountAsync()).Should().Be(1);
        pending.Preview!.After.Should().NotBeNull();
        (await services.GetRequiredService<IToolApprovalService>().DecideAsync(pending.ApprovalRequestId!.Value, new(ToolApprovalDecisionType.Approve)))
            .Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        WorkflowTriggerDetail saved = ToolResult<WorkflowTriggerDetail>(await save.InvokeAsync(new AIFunctionArguments(arguments)));
        saved.Status.Should().Be("Draft");
        AIFunction activate = WorkflowTriggerTools.CreateAll(services).OfType<AIFunction>().Single(tool => tool.Name == "aonik_request_activate_workflow_trigger");
        var command = new Dictionary<string, object?> { ["slug"] = "invoice-route", ["bindingId"] = saved.Id,
            ["command"] = new WorkflowTriggerCommand(saved.ExpectedVersion, saved.DraftHash) };
        ToolApprovalQueuedResult queued = ToolResult<ToolApprovalQueuedResult>(await activate.InvokeAsync(new AIFunctionArguments(command)));
        (await services.GetRequiredService<AgentsDbContext>().WorkflowTriggerBindings.SingleAsync(value => value.Id == saved.Id)).Status.Should().Be("Draft");
        (await services.GetRequiredService<AgentsDbContext>().Proposals.SingleAsync(value => value.Id == queued.ProposalId)).Status.Should().Be(ProposalStatus.Proposed);
    }

    [Fact]
    public async Task Schedule_Should_SkipAMissedOccurrenceUnlessCatchUpWasExplicitlyApproved()
    {
        using var host = new Host();
        (WorkflowTriggerDetail source, _) = await ActivateTriggerAsync(host, (definition, template) => definition with {
            Source = "Schedule", Subject = new("Invoice", "Fixed", [template.SubjectId!.Value]),
            Schedule = new("0 * * * * ?", "Europe/London", host.Clock.UtcNow, host.Clock.UtcNow.AddDays(1), 10) });
        TaskActionContext context = await ScheduleContextAsync(host, source, host.Clock.UtcNow.AddMinutes(1), host.Clock.UtcNow.AddMinutes(4));
        host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(4);
        using IServiceScope scope = host.Scope();

        TaskActionResult result = await scope.ServiceProvider.GetRequiredService<WorkflowTriggerScheduleTaskHandler>().ExecuteAsync(context);

        result.Outcome.Should().Be(TaskActionOutcome.Skipped); result.Error.Should().Be("MissedOccurrenceSkipped");
        (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.CountAsync(value => value.TriggerBindingId == source.Id)).Should().Be(0);
    }

    [Fact]
    public async Task ScheduleCatchUp_Should_KeepItsApprovedBatchAndOccurrenceIdentityAcrossDeferredRetries()
    {
        using var host = new Host();
        (WorkflowTriggerDetail source, _) = await ActivateTriggerAsync(host, (definition, template) => definition with {
            Source = "Schedule", Subject = new("Invoice", "Fixed", [template.SubjectId!.Value]),
            Schedule = new("0 * * * * ?", "UTC", host.Clock.UtcNow, host.Clock.UtcNow.AddDays(1), 2, 2) });
        TaskActionContext context = await ScheduleContextAsync(host, source, host.Clock.UtcNow.AddMinutes(1), host.Clock.UtcNow.AddMinutes(4));
        host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(4);
        using IServiceScope scope = host.Scope(); var services = scope.ServiceProvider;
        var handler = services.GetRequiredService<WorkflowTriggerScheduleTaskHandler>();
        (await handler.ExecuteAsync(context)).Outcome.Should().Be(TaskActionOutcome.Deferred);
        var database = services.GetRequiredService<AgentsDbContext>();
        var original = await database.WorkflowRuns.SingleAsync(value => value.TriggerBindingId == source.Id);
        var runs = services.GetRequiredService<IWorkflowRunService>();
        await runs.CancelAsync(original.Id, new((await runs.GetAsync(original.Id))!.ExpectedVersion));
        host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(10);

        (await handler.ExecuteAsync(context)).Outcome.Should().Be(TaskActionOutcome.Succeeded);
        (await handler.ExecuteAsync(context)).Outcome.Should().Be(TaskActionOutcome.Succeeded);

        var admitted = await database.WorkflowRuns.Where(value => value.TriggerBindingId == source.Id).ToListAsync();
        admitted.Should().HaveCount(2); admitted.Select(value => value.SourceEventId).Distinct().Should().HaveCount(2);
        admitted.Select(value => value.SourceOccurredAtUtc).Should().BeEquivalentTo(new DateTime?[] { context.ScheduledForUtc, context.ScheduledForUtc.AddMinutes(1) });
        var nextOccurrence = context with { ScheduledForUtc = context.ScheduledForUtc.AddMinutes(2) };
        await handler.ExecuteAsync(nextOccurrence);
        (await database.WorkflowRuns.CountAsync(value => value.TriggerBindingId == source.Id)).Should().Be(2, "the configured occurrence limit includes catch-up admissions");
    }

    [Fact]
    public async Task InvoiceEvents_Should_AdmitOnceAcrossDuplicateWakes_ThenWaitForTheRealHumanGate()
    {
        using var host = new Host();
        (WorkflowTriggerDetail source, WorkflowRun template) = await ActivateTriggerAsync(host);
        Guid runId;
        using (IServiceScope scope = host.Scope())
        {
            var services = scope.ServiceProvider;
            var message = new InvoiceLifecycleChangedEvent(Guid.NewGuid(), host.Clock.UtcNow, template.TenantId, template.SubjectId!.Value, "Created");
            var events = services.GetRequiredService<InvoiceWorkflowTriggerHandler>();
            await events.HandleAsync(message); await events.HandleAsync(message);
            List<WorkflowTriggerAdmissionRequestedEvent> wakes = await TriggerWakesAsync(scope.ServiceProvider);
            wakes.Should().HaveCount(2);
            var handler = services.GetRequiredService<WorkflowTriggerAdmissionTaskHandler>();
            foreach (var wake in wakes)
            {
                await services.GetRequiredService<WorkflowTriggerAdmissionRequestedHandler>().HandleAsync(wake);
                host.Actor.Authenticated = false;
                TaskActionResult result = await handler.ExecuteAsync(AdmissionContext(wake));
                result.Outcome.Should().Be(TaskActionOutcome.Succeeded);
            }
            var database = services.GetRequiredService<AgentsDbContext>();
            WorkflowRun run = await database.WorkflowRuns.SingleAsync(run => run.TriggerBindingId == source.Id);
            run.SourceEventId.Should().Be(message.EventId); run.TriggerDefinitionHash.Should().Be(source.ActiveHash);
            run.InitiatingUserId.Should().BeNull(); run.ActorType.Should().Be("AutomationIdentity");
            (await database.WorkflowTriggerBindings.SingleAsync()).AdmittedRuns.Should().Be(1);
            runId = run.Id;
        }
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        host.Actor.Authenticated = true; host.Actor.Id = host.Reviewer;
        using (IServiceScope scope = host.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AgentsDbContext>();
            WorkflowRun run = await database.WorkflowRuns.SingleAsync(run => run.Id == runId);
            Guid requestId = (await database.ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId == run.PendingStepRunId)).Id;
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(requestId, new(ToolApprovalDecisionType.Approve)))
                .Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        }
        host.Actor.Authenticated = false;
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Completed);
        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Issued");
    }

    [Fact]
    public async Task InvoiceTrigger_Should_SkipSelfGeneratedEventsAfterTheWorkItemIsVerified()
    {
        using var host = new Host();
        (WorkflowTriggerDetail source, WorkflowRun template) = await ActivateTriggerAsync(host);
        using IServiceScope scope = host.Scope();
        IServiceProvider services = scope.ServiceProvider;
        var events = services.GetRequiredService<InvoiceWorkflowTriggerHandler>();
        var message = new InvoiceLifecycleChangedEvent(Guid.NewGuid(), host.Clock.UtcNow, template.TenantId,
            template.SubjectId!.Value, "Created", CausationRunId: template.Id);
        await events.HandleAsync(message);
        WorkflowTriggerAdmissionRequestedEvent wake = (await TriggerWakesAsync(services)).Should().ContainSingle().Which;
        await services.GetRequiredService<WorkflowTriggerAdmissionRequestedHandler>().HandleAsync(wake);
        host.Actor.Authenticated = false;

        TaskActionResult result = await services.GetRequiredService<WorkflowTriggerAdmissionTaskHandler>().ExecuteAsync(AdmissionContext(wake));

        result.Outcome.Should().Be(TaskActionOutcome.Skipped);
        result.Error.Should().Be("SelfGeneratedTriggerIgnored");
        var database = services.GetRequiredService<AgentsDbContext>();
        (await database.WorkflowRuns.AnyAsync(run => run.TriggerBindingId == source.Id)).Should().BeFalse();
        (await database.ToolApprovalRequests.AnyAsync(request => request.WorkflowStepRunId.HasValue)).Should().BeFalse();
    }

    [Fact]
    public async Task InvoiceTrigger_Should_SkipRecursiveEvents_EvenWhenSelfGeneratedEventsAreAllowed()
    {
        using var host = new Host();
        (WorkflowTriggerDetail source, WorkflowRun template) = await ActivateTriggerAsync(host,
            (definition, _) => definition with { Limits = definition.Limits with { IgnoreSelfGenerated = false } });
        using IServiceScope scope = host.Scope();
        IServiceProvider services = scope.ServiceProvider;
        var events = services.GetRequiredService<InvoiceWorkflowTriggerHandler>();
        var handler = services.GetRequiredService<WorkflowTriggerAdmissionTaskHandler>();
        var message = new InvoiceLifecycleChangedEvent(Guid.NewGuid(), host.Clock.UtcNow, template.TenantId,
            template.SubjectId!.Value, "Created");
        await events.HandleAsync(message);
        host.Actor.Authenticated = false;
        WorkflowTriggerAdmissionRequestedEvent first = (await TriggerWakesAsync(services)).Should().ContainSingle().Which;
        (await handler.ExecuteAsync(AdmissionContext(first))).Outcome.Should().Be(TaskActionOutcome.Succeeded);
        var database = services.GetRequiredService<AgentsDbContext>();
        WorkflowRun original = await database.WorkflowRuns.SingleAsync(run => run.TriggerBindingId == source.Id);
        original.WorkflowVersionId.Should().Be(template.WorkflowVersionId);
        var nested = message with { EventId = Guid.NewGuid(), CausationRunId = original.Id };
        await events.HandleAsync(nested);
        WorkflowTriggerAdmissionRequestedEvent recursiveWake = (await TriggerWakesAsync(services)).Single(wake => wake.SourceEventId == nested.EventId);

        TaskActionResult result = await handler.ExecuteAsync(AdmissionContext(recursiveWake));

        result.Outcome.Should().Be(TaskActionOutcome.Skipped);
        result.Error.Should().Be("TriggerCausationLimit");
        (await database.WorkflowRuns.CountAsync(run => run.TriggerBindingId == source.Id)).Should().Be(1);
        (await database.ToolApprovalRequests.AnyAsync(request => request.WorkflowStepRunId.HasValue)).Should().BeFalse();
    }

    [Fact]
    public async Task InvoiceTrigger_Should_DiscardOldEventsAndProtectedWakes_AfterReactivation()
    {
        using var host = new Host();
        (WorkflowTriggerDetail source, WorkflowRun template) = await ActivateTriggerAsync(host);
        using IServiceScope scope = host.Scope();
        IServiceProvider services = scope.ServiceProvider;
        var events = services.GetRequiredService<InvoiceWorkflowTriggerHandler>();
        var original = new InvoiceLifecycleChangedEvent(Guid.NewGuid(), host.Clock.UtcNow, template.TenantId,
            template.SubjectId!.Value, "Created");
        await events.HandleAsync(original);
        WorkflowTriggerAdmissionRequestedEvent retiredWake = (await TriggerWakesAsync(services)).Should().ContainSingle().Which;
        var triggers = services.GetRequiredService<WorkflowTriggerService>();
        var approvals = services.GetRequiredService<IToolApprovalService>();
        host.Clock.UtcNow = host.Clock.UtcNow.AddMinutes(1);
        foreach (bool disabling in new[] { true, false })
        {
            WorkflowTriggerDetail binding = (await triggers.GetAsync("invoice-route", source.Id))!;
            var proposal = await triggers.RequestAsync("invoice-route", source.Id,
                new(binding.ExpectedVersion, disabling ? binding.ActiveHash! : binding.DraftHash), disabling);
            (await approvals.DecideAsync(proposal.ApprovalRequestId, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        }
        (await triggers.GetAsync("invoice-route", source.Id))!.ActiveRevision.Should().Be(2);
        host.Actor.Authenticated = false;

        await events.HandleAsync(original);
        TaskActionResult delayed = await services.GetRequiredService<WorkflowTriggerAdmissionTaskHandler>().ExecuteAsync(AdmissionContext(retiredWake));

        delayed.Outcome.Should().Be(TaskActionOutcome.Skipped);
        delayed.Error.Should().Be("TriggerRevisionNotActive");
        (await TriggerWakesAsync(services)).Should().ContainSingle().Which.Revision.Should().Be(1);
        (await services.GetRequiredService<AgentsDbContext>().WorkflowRuns.AnyAsync(run => run.TriggerBindingId == source.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task TriggerCapacity_Should_DeferAnotherSourceEvent_AndAdmitItWhenTheOriginalRunIsCancelled()
    {
        using var host = new Host();
        (WorkflowTriggerDetail source, WorkflowRun template) = await ActivateTriggerAsync(host);
        using IServiceScope scope = host.Scope();
        var admissions = scope.ServiceProvider.GetRequiredService<WorkflowTriggerAdmissionService>();
        var first = new WorkflowTriggerAdmissionPayload(source.Id, source.ActiveRevision!.Value, Guid.NewGuid(), host.Clock.UtcNow, "Invoice", template.SubjectId!.Value, null);
        Guid originalId = JsonSerializer.Deserialize<JsonElement>((await admissions.AdmitAsync(first)).ResultJson!).GetProperty("runId").GetGuid();
        var second = first with { SourceEventId = Guid.NewGuid() };
        TaskActionResult waiting = await admissions.AdmitAsync(second);
        waiting.Outcome.Should().Be(TaskActionOutcome.Deferred); waiting.DeferredUntilUtc.Should().Be(host.Clock.UtcNow.AddMinutes(1));
        var runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
        WorkflowRunDetail current = (await runs.GetAsync(originalId))!;
        await runs.CancelAsync(originalId, new(current.ExpectedVersion));
        (await admissions.AdmitAsync(second)).Outcome.Should().Be(TaskActionOutcome.Succeeded);
        (await scope.ServiceProvider.GetRequiredService<AgentsDbContext>().WorkflowRuns.CountAsync(run => run.TriggerBindingId == source.Id)).Should().Be(2);
    }

    [Fact]
    public async Task TriggerDisable_Should_CancelAPendingFinancialApproval_BeforeAnotherEffect()
    {
        using var host = new Host();
        (WorkflowTriggerDetail source, WorkflowRun template) = await ActivateTriggerAsync(host);
        Guid runId;
        using (IServiceScope scope = host.Scope())
        {
            TaskActionResult admitted = await scope.ServiceProvider.GetRequiredService<WorkflowTriggerAdmissionService>().AdmitAsync(
                new(source.Id, source.ActiveRevision!.Value, Guid.NewGuid(), host.Clock.UtcNow, "Invoice", template.SubjectId!.Value, null));
            runId = JsonSerializer.Deserialize<JsonElement>(admitted.ResultJson!).GetProperty("runId").GetGuid();
        }
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.WaitingForApproval);
        using (IServiceScope scope = host.Scope())
        {
            var triggers = scope.ServiceProvider.GetRequiredService<WorkflowTriggerService>();
            WorkflowTriggerDetail latest = (await triggers.GetAsync("invoice-route", source.Id))!;
            WorkflowTriggerGovernanceResult disable = await triggers.RequestAsync("invoice-route", source.Id, new(latest.ExpectedVersion, latest.ActiveHash!), true);
            (await scope.ServiceProvider.GetRequiredService<IToolApprovalService>().DecideAsync(disable.ApprovalRequestId, new(ToolApprovalDecisionType.Approve)))
                .Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        }
        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Cancelled);
        using IServiceScope final = host.Scope();
        ToolApprovalRequest approval = await final.ServiceProvider.GetRequiredService<AgentsDbContext>().ToolApprovalRequests.SingleAsync(request => request.WorkflowStepRunId != null);
        approval.Status.Should().Be(ToolApprovalRequestStatus.Cancelled); approval.DecidedByUserId.Should().BeNull();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Reactivation_Should_NeverReviveRunsFromADisabledRevision_BeforeCancellationDelivery()
    {
        using var host = new Host(); (WorkflowTriggerDetail source, WorkflowRun template) = await ActivateTriggerAsync(host);
        Guid runId;
        using (IServiceScope scope = host.Scope())
        {
            var services = scope.ServiceProvider;
            TaskActionResult result = await services.GetRequiredService<WorkflowTriggerAdmissionService>().AdmitAsync(new(source.Id, source.ActiveRevision!.Value,
                Guid.NewGuid(), host.Clock.UtcNow, "Invoice", template.SubjectId!.Value, null));
            runId = JsonSerializer.Deserialize<JsonElement>(result.ResultJson!).GetProperty("runId").GetGuid();
            var triggers = services.GetRequiredService<WorkflowTriggerService>(); var approvals = services.GetRequiredService<IToolApprovalService>();
            foreach (bool disabling in new[] { true, false })
            {
                WorkflowTriggerDetail current = (await triggers.GetAsync("invoice-route", source.Id))!;
                var proposal = await triggers.RequestAsync("invoice-route", source.Id, new(current.ExpectedVersion, disabling ? current.ActiveHash! : current.DraftHash), disabling);
                (await approvals.DecideAsync(proposal.ApprovalRequestId, new(ToolApprovalDecisionType.Approve))).Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
            }
            (await triggers.GetAsync("invoice-route", source.Id))!.ActiveRevision.Should().Be(2);
        }

        (await host.Continue(runId)).Status.Should().Be(WorkflowRunStatuses.Cancelled);

        using IServiceScope final = host.Scope();
        (await final.ServiceProvider.GetRequiredService<FinanceDbContext>().Invoices.SingleAsync()).Status.Should().Be("Draft");
    }

    [Fact]
    public async Task TriggerTaskProof_Should_RejectAChangedTenantTaskOrInvoice()
    {
        using var host = new Host();
        (WorkflowTriggerDetail source, WorkflowRun template) = await ActivateTriggerAsync(host);
        using IServiceScope scope = host.Scope();
        var proofs = scope.ServiceProvider.GetRequiredService<WorkflowTriggerTaskProtector>();
        Guid taskId = Guid.NewGuid();
        var payload = new WorkflowTriggerAdmissionPayload(source.Id, 1, Guid.NewGuid(), host.Clock.UtcNow, "Invoice", template.SubjectId!.Value, null);
        string encoded = proofs.ProtectAdmission(payload, template.TenantId, taskId);
        Action otherTenant = () => proofs.ReadAdmission(encoded, Guid.NewGuid(), taskId);
        Action otherTask = () => proofs.ReadAdmission(encoded, template.TenantId, Guid.NewGuid());
        otherTenant.Should().Throw<ArgumentException>(); otherTask.Should().Throw<ArgumentException>();
        TaskActionContext forged = new(template.TenantId, taskId, Guid.NewGuid(), TaskKinds.ScheduledAction, "WorkflowTriggerBinding", source.Id,
            TaskAssigneeTypes.System, null, null, host.Clock.UtcNow, JsonSerializer.Serialize(payload with { SubjectId = Guid.NewGuid() }, WorkflowDefinitionCodec.JsonOptions));
        Func<Task> substitute = () => scope.ServiceProvider.GetRequiredService<WorkflowTriggerAdmissionTaskHandler>().ExecuteAsync(forged);
        await substitute.Should().ThrowAsync<ArgumentException>();
    }

    private static async Task<(WorkflowTriggerDetail, WorkflowRun)> ActivateTriggerAsync(Host host, Func<WorkflowTriggerDefinition, WorkflowRun, WorkflowTriggerDefinition>? configure = null)
    {
        Guid templateId = await host.StartAutomation();
        using IServiceScope scope = host.Scope();
        var services = scope.ServiceProvider;
        WorkflowRun template = await services.GetRequiredService<AgentsDbContext>().WorkflowRuns.SingleAsync(run => run.Id == templateId);
        var definition = new WorkflowTriggerDefinition(1, "InvoiceCreated", template.WorkflowVersionId!.Value, template.ActorId!.Value,
            new("Invoice", "EventSubject", []), new(MaximumStartsPerHour: 20, MaximumConcurrentRuns: 2));
        if (configure is not null) definition = configure(definition, template);
        var triggers = services.GetRequiredService<WorkflowTriggerService>();
        WorkflowTriggerDetail draft = await triggers.SaveAsync("invoice-route", null, new("Invoice creation route", definition));
        WorkflowTriggerGovernanceResult requested = await triggers.RequestAsync("invoice-route", draft.Id, new(draft.ExpectedVersion, draft.DraftHash), false);
        (await services.GetRequiredService<IToolApprovalService>().DecideAsync(requested.ApprovalRequestId, new(ToolApprovalDecisionType.Approve)))
            .Outcome.Should().Be(ToolApprovalDecisionOutcome.Approved);
        WorkflowTriggerDetail active = (await triggers.GetAsync("invoice-route", draft.Id))!;
        active.Status.Should().Be("Active");
        return (active, template);
    }
    private static async Task<List<WorkflowTriggerAdmissionRequestedEvent>> TriggerWakesAsync(IServiceProvider services)
        => (await services.GetRequiredService<AgentsDbContext>().Set<OutboxMessage>().Where(message => message.EventType == typeof(WorkflowTriggerAdmissionRequestedEvent).FullName).ToListAsync())
            .Select(message => JsonSerializer.Deserialize<WorkflowTriggerAdmissionRequestedEvent>(message.Payload, OutboxSerialization.Options)!).ToList();
    private static TaskActionContext AdmissionContext(WorkflowTriggerAdmissionRequestedEvent message) => new(message.TenantId, message.EventId, Guid.NewGuid(),
        TaskKinds.ScheduledAction, "WorkflowTriggerBinding", message.BindingId, TaskAssigneeTypes.System, null, null, message.SourceOccurredAtUtc, message.TaskProof);
    private static async Task<TaskActionContext> ScheduleContextAsync(Host host, WorkflowTriggerDetail source, DateTime occurrence, DateTime observed)
    {
        using IServiceScope scope = host.Scope(); var services = scope.ServiceProvider;
        OutboxMessage stored = await services.GetRequiredService<AgentsDbContext>().Set<OutboxMessage>().SingleAsync(value => value.EventType == typeof(WorkflowTriggerChangedEvent).FullName);
        WorkflowTriggerChangedEvent message = JsonSerializer.Deserialize<WorkflowTriggerChangedEvent>(stored.Payload, OutboxSerialization.Options)!;
        await services.GetRequiredService<WorkflowTriggerChangedHandler>().HandleAsync(message);
        ScheduleTaskRequest task = host.ScheduledTasks.Single(value => value.ActionType == "workflow_trigger_schedule");
        return new(message.TenantId, task.TaskId!.Value, Guid.NewGuid(), TaskKinds.ScheduledAction, "WorkflowTriggerBinding", source.Id, TaskAssigneeTypes.System,
            null, null, occurrence, task.ActionPayloadJson, observed);
    }
    private static T ToolResult<T>(object? result) => result is T typed ? typed : JsonSerializer.SerializeToElement(result, WorkflowDefinitionCodec.JsonOptions).Deserialize<T>(WorkflowDefinitionCodec.JsonOptions)!;
}
