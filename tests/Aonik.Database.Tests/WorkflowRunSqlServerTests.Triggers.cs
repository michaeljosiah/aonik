using System.Text.Json;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Entities;
using Aonik.Agents.Entities.Workflows;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.Agents.Services.Workflows;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Integrations;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Workflows;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

namespace Aonik.Database.Tests;

public partial class WorkflowRunSqlServerTests
{
    [SkippableFact]
    public async Task TriggerAdmission_Should_DeduplicateConcurrentSourceWakes_WithOneAtomicReceiptAndOutbox()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        var fixture = await SeedTriggerAsync();
        Guid sourceEventId = Guid.NewGuid();
        // Source time is part of the exact admission hash and survives worker retries.
        DateTime occurredAt = DateTime.UtcNow;
        var source = new WorkflowTriggerSource(fixture.BindingId, 1, fixture.ProposalId, fixture.Hash, sourceEventId, occurredAt, null);
        async Task<Guid> FixedStartAsync()
        {
            for (int attempt = 0; ; attempt++)
            {
                await using AgentsDbContext context = Context(fixture.TenantId);
                await using var platform = new PlatformDbContext(database.CreateOptions<PlatformDbContext>(), new TestTenantProvider(fixture.TenantId));
                var service = Service(context, fixture.TenantId, fixture.UserId, fixture.SubjectId, new AutomationIdentityAuthorization(platform, new TestTenantProvider(fixture.TenantId), new Clock()));
                try { return await service.AdmitTriggeredAsync(fixture.ActorId, fixture.VersionId, "Invoice", fixture.SubjectId, "same-source", source); }
                catch (WorkflowConflictException) when (attempt < 2) { }
            }
        }
        Guid[] ids = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => FixedStartAsync()));
        ids.Distinct().Should().ContainSingle();
        await using AgentsDbContext verify = Context(fixture.TenantId);
        (await verify.WorkflowRuns.CountAsync()).Should().Be(1);
        (await verify.WorkflowTriggerBindings.SingleAsync()).AdmittedRuns.Should().Be(1);
        (await verify.Set<OutboxMessage>().CountAsync(value => value.TenantId == fixture.TenantId)).Should().Be(1);
        (await verify.WorkflowRuns.SingleAsync()).SourceEventId.Should().Be(sourceEventId);
    }

    [SkippableFact]
    public async Task DisabledBinding_Should_RollBackAStaleAdmissionAndItsWakeupTogether()
    {
        Skip.IfNot(database.IsAvailable, database.SkipReason);
        var fixture = await SeedTriggerAsync();
        await using AgentsDbContext stale = Context(fixture.TenantId);
        await stale.WorkflowTriggerBindings.SingleAsync();
        await using (AgentsDbContext concurrent = Context(fixture.TenantId))
        {
            (await concurrent.WorkflowTriggerBindings.SingleAsync()).Status = "Disabled";
            await concurrent.SaveChangesAsync();
        }
        await using var platform = new PlatformDbContext(database.CreateOptions<PlatformDbContext>(), new TestTenantProvider(fixture.TenantId));
        var service = Service(stale, fixture.TenantId, fixture.UserId, fixture.SubjectId, new AutomationIdentityAuthorization(platform, new TestTenantProvider(fixture.TenantId), new Clock()));
        Func<Task> admit = () => service.AdmitTriggeredAsync(fixture.ActorId, fixture.VersionId, "Invoice", fixture.SubjectId, "stale-source",
            new(fixture.BindingId, 1, fixture.ProposalId, fixture.Hash, Guid.NewGuid(), DateTime.UtcNow, null));
        await admit.Should().ThrowAsync<WorkflowConflictException>();
        await using AgentsDbContext verify = Context(fixture.TenantId);
        (await verify.WorkflowRuns.CountAsync()).Should().Be(0);
        (await verify.Set<OutboxMessage>().CountAsync(value => value.TenantId == fixture.TenantId)).Should().Be(0);
    }

    private async Task<TriggerFixture> SeedTriggerAsync()
    {
        Guid tenantId = Guid.NewGuid(), userId = Guid.NewGuid(), actorId = Guid.NewGuid(), subjectId = Guid.NewGuid();
        await using AgentsDbContext seed = Context(tenantId);
        var workflows = new WorkflowService(seed, new TestTenantProvider(tenantId), new Clock(), new(new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId), new AllowAllPermissionService()));
        await workflows.CreateAsync(new("sql-trigger-route", "Invoice route", "", "Draft", "v0.1", false, "", null, [],
            [new("start", "Trigger", "Start", "", "", 0, 0, "{}"), new("end", "End", "Done", "", "", 1, 0, "{}")], [new("start", "end", 0, "")], null));
        Workflow workflow = await seed.Workflows.SingleAsync(); WorkflowVersion version = await seed.WorkflowVersions.SingleAsync();
        version.PublishedAt = DateTime.UtcNow; version.PublishedByUserId = userId; workflow.PublishedVersionId = version.Id; workflow.State = "Active";
        var definition = new WorkflowTriggerDefinition(1, "InvoiceCreated", version.Id, actorId, new("Invoice", "EventSubject", []), new());
        string json = JsonSerializer.Serialize(definition, WorkflowDefinitionCodec.JsonOptions); string hash = WorkflowDefinitionCodec.Hash(json);
        var binding = new WorkflowTriggerBinding { TenantId = tenantId, WorkflowId = workflow.Id, Name = "SQL source", DraftJson = json, DraftHash = hash,
            DraftRevision = 1, ActiveJson = json, ActiveHash = hash, ActiveRevision = 1, ActiveSource = "InvoiceCreated", Status = "Active", ActivatedAt = DateTime.UtcNow.AddMinutes(-1),
            ActiveAutomationIdentityId = actorId, ConfiguredByUserId = userId };
        var proposal = new Proposal { TenantId = tenantId, ProposalType = WorkflowTriggerApprovalManifest.ActivateType, Status = ProposalStatus.Applied, ApprovedByUserId = userId,
            ApprovedAt = DateTime.UtcNow, PayloadJson = JsonSerializer.Serialize(new { bindingId = binding.Id, command = new { definitionHash = hash } }) };
        binding.ActivationProposalId = proposal.Id; seed.WorkflowTriggerBindings.Add(binding); seed.Proposals.Add(proposal); await seed.SaveChangesAsync();
        await using var platform = new PlatformDbContext(database.CreateOptions<PlatformDbContext>(), new TestTenantProvider(tenantId));
        platform.Tenants.Add(new Tenant { Id = tenantId, Name = $"Trigger SQL {tenantId:N}", Status = "Active" });
        platform.AutomationIdentities.Add(new AutomationIdentity { Id = actorId, TenantId = tenantId, Name = "Source actor", AccountableUserId = userId, ConfiguredByUserId = userId,
            PermissionsJson = ConnectorJson.Serialize(new[] { "Workflow.Read", "Workflow.Run", "Invoice.Read" }),
            GrantsJson = ConnectorJson.Serialize(new[] { new AutomationWorkflowGrant(version.Id, "Invoice", true, [], []) }) });
        await platform.SaveChangesAsync();
        return new(tenantId, userId, actorId, subjectId, version.Id, binding.Id, proposal.Id, hash);
    }
    private sealed record TriggerFixture(Guid TenantId, Guid UserId, Guid ActorId, Guid SubjectId, Guid VersionId, Guid BindingId, Guid ProposalId, string Hash);
}
